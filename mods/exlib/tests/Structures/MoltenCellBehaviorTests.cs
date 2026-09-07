using ExpandedLib.Industry.Molten;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The composable molten-cell behaviour (<see cref="BEBehaviorMoltenCell"/>): the per-cell metal state
/// and operations a mega-block footprint cell (the sand casting bed's runners and molds) carries by
/// composition. Covers config parsing, the push/drain/soak/thermal contract, melt-point
/// classification, recovery, and the save round-trip. The fixture melts iron at 1500 C, so liquid is
/// above 0.8x = 1200 and hardened below 0.3x = 450.
/// </summary>
public class MoltenCellBehaviorTests {
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";
  private const float IronMelt = 1500f;

  #region Fixture

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, IronMelt);
    world.RegisterItem(Steel, IronMelt);
    world.RegisterItem("game:metalbit-iron");
    return world;
  }

  // A cell behaviour attached to a placed, API-linked filler BE, configured from the given props.
  private static BEBehaviorMoltenCell NewCell(
    TestWorld world,
    string props = "{}"
  ) {
    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      70
    );
    var be = new BlockEntityStructureFiller();
    world.Place(new BlockPos(0, 0, 0), filler, be);
    world.Attach(be);
    var cell = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(cell);
    cell.ConfigureFromFiller(null, null, new JsonObject(JToken.Parse(props)));
    return cell;
  }

  #endregion

  #region Config

  [Fact]
  public void Config_reads_capacity_flow_source_and_drain_fitting() {
    var cell = NewCell(
      NewWorld(),
      "{ \"capacity\": 300, \"flowSource\": true, \"drainFitting\": true }"
    );

    Assert.Equal(300, cell.MaxUnitCapacity);
    Assert.True(cell.IsFlowSource);
    Assert.True(cell.AcceptsSubMinimumFlow);
  }

  [Fact]
  public void SetCapacity_overrides_the_declared_capacity_and_clears_back_to_it() {
    var cell = NewCell(NewWorld(), "{ \"capacity\": 200 }");
    Assert.Equal(200, cell.MaxUnitCapacity);

    // A rammed mold pattern raises the cavity size at runtime,
    cell.SetCapacity(136);
    Assert.Equal(136, cell.MaxUnitCapacity);

    // shake-out drops the override back to the declared capacity.
    cell.ClearCapacity();
    Assert.Equal(200, cell.MaxUnitCapacity);
  }

  [Fact]
  public void SetCapacity_of_zero_or_less_reverts_to_the_declared_capacity() {
    var cell = NewCell(NewWorld(), "{ \"capacity\": 200 }");
    cell.SetCapacity(0);
    Assert.Equal(200, cell.MaxUnitCapacity);
  }

  [Fact]
  public void The_pattern_capacity_override_survives_a_save_round_trip() {
    var world = NewWorld();
    var cell = NewCell(world, "{ \"capacity\": 200 }");
    cell.SetCapacity(297);

    var tree = new TreeAttribute();
    cell.ToTreeAttributes(tree);

    var restored = NewCell(world, "{ \"capacity\": 200 }");
    restored.FromTreeAttributes(tree, world.World);
    Assert.Equal(297, restored.MaxUnitCapacity);
  }

  [Fact]
  public void Config_defaults_a_plain_cell() {
    var cell = NewCell(NewWorld());

    Assert.Equal(BEBehaviorMoltenCell.DefaultCapacity, cell.MaxUnitCapacity);
    Assert.False(cell.IsFlowSource);
    Assert.False(cell.AcceptsSubMinimumFlow);
  }

  #endregion

  #region Push / drain

  [Fact]
  public void PushMetal_fills_up_to_capacity_and_reports_what_it_accepted() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");

    int accepted = cell.PushMetalRaw(150, Iron, 1500f, w.World);

    Assert.Equal(100, accepted); // capped at capacity
    Assert.Equal(100, cell.CellAmount);
    Assert.Equal(Iron, cell.CellMetalType);
  }

  [Fact]
  public void PushMetal_refuses_a_different_metal_while_occupied() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 1500f, w.World);

    int accepted = cell.PushMetalRaw(40, Steel, 1500f, w.World);

    Assert.Equal(0, accepted);
    Assert.Equal(40, cell.CellAmount);
    Assert.Equal(Iron, cell.CellMetalType);
  }

  [Fact]
  public void PushMetal_temperature_averages_same_metal() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 1600f, w.World);

    cell.PushMetalRaw(40, Iron, 1200f, w.World);

    // (40*1600 + 40*1200) / 80 = 1400
    Assert.Equal(1400f, cell.CellTemperature, 1);
  }

  [Fact]
  public void DrainMetal_removes_units_and_empties_at_zero() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(100, Iron, 1500f, w.World);

    Assert.Equal(30, cell.DrainMetal(30));
    Assert.Equal(70, cell.CellAmount);

    cell.DrainMetal(70);
    Assert.Equal(0, cell.CellAmount);
    Assert.Equal("", cell.CellMetalType);
    Assert.True(cell.IsCellEmpty);
  }

  [Fact]
  public void SoakHeat_raises_temperature_without_adding_volume() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 1200f, w.World);

    Assert.True(cell.SoakHeat(w.World, 1500f));
    Assert.Equal(1500f, cell.CellTemperature, 1);
    Assert.Equal(40, cell.CellAmount); // volume unchanged

    // Not hotter than current -> no soak.
    Assert.False(cell.SoakHeat(w.World, 1400f));
  }

  #endregion

  #region Thermal + classification

  [Fact]
  public void UpdateThermal_latches_solidified_below_the_melting_point() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 800f, w.World); // already below the 1500 melt

    Assert.False(cell.Solidified);
    cell.UpdateThermal(w.World);
    Assert.True(cell.Solidified);
  }

  [Fact]
  public void UpdateThermal_never_latches_when_the_cell_does_not_solidify() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100, \"solidifies\": false }");
    cell.PushMetalRaw(40, Iron, 800f, w.World);

    cell.UpdateThermal(w.World);

    Assert.False(cell.Solidified);
  }

  [Fact]
  public void A_hot_cell_stays_liquid_through_a_thermal_tick() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 1600f, w.World);

    cell.UpdateThermal(w.World);

    Assert.False(cell.Solidified);
    Assert.True(cell.HasMoltenMetal);
  }

  [Theory]
  [InlineData(1300f, MoltenState.Liquid)]
  [InlineData(1100f, MoltenState.Cooling)]
  [InlineData(400f, MoltenState.Hardened)]
  public void CellState_classifies_against_the_melting_point(
    float temp,
    MoltenState expected
  ) {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, temp, w.World);

    Assert.Equal(expected, cell.CellState);
    Assert.Equal(expected == MoltenState.Hardened, cell.IsHardened);
  }

  [Fact]
  public void GlowLightLevel_is_zero_when_empty_and_positive_when_hot() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    Assert.Equal(0, cell.GlowLightLevel);

    cell.PushMetalRaw(40, Iron, 1500f, w.World);
    Assert.True(cell.GlowLightLevel > 0);
  }

  #endregion

  #region Recovery + clearing

  [Fact]
  public void GetRecoveryDrop_yields_the_metal_bit() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(50, Iron, 900f, w.World);

    ItemStack? drop = cell.GetRecoveryDrop(w.World);

    Assert.NotNull(drop);
    Assert.Equal("game:metalbit-iron", drop!.Collectible.Code.ToString());
    Assert.Equal(10, drop.StackSize); // 50 / 5 units each
  }

  [Fact]
  public void ClearContents_empties_the_cell_and_lifts_the_latch() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(40, Iron, 800f, w.World);
    cell.UpdateThermal(w.World);
    Assert.True(cell.Solidified);

    cell.ClearContents();

    Assert.Equal(0, cell.CellAmount);
    Assert.False(cell.Solidified);
    Assert.Equal("", cell.CellMetalType);
  }

  #endregion

  #region Serialization

  [Fact]
  public void State_round_trips_through_the_save_tree() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");
    cell.PushMetalRaw(60, Iron, 1300f, w.World);

    var tree = new TreeAttribute();
    cell.ToTreeAttributes(tree);

    var restored = NewCell(w, "{ \"capacity\": 100 }");
    restored.FromTreeAttributes(tree, w.World);

    Assert.Equal(60, restored.CellAmount);
    Assert.Equal(Iron, restored.CellMetalType);
    Assert.Equal(1300f, restored.CellTemperature, 1);
  }

  [Fact]
  public void An_empty_cell_never_persists_as_solidified() {
    var w = NewWorld();
    var cell = NewCell(w, "{ \"capacity\": 100 }");

    // A phantom flag from an old save: solidified with no metal.
    var tree = new TreeAttribute();
    tree.SetInt("mc_amount", 0);
    tree.SetString("mc_type", Iron);
    tree.SetBool("mc_solid", true);
    cell.FromTreeAttributes(tree, w.World);

    Assert.False(cell.Solidified);
    Assert.Equal("", cell.CellMetalType);
  }

  #endregion
}
