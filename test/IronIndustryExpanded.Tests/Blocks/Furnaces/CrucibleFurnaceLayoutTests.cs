using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The crucible furnace's drawing: one column - ash pit, hearth, damper, flue - and then the first course
/// of a chimney the player carries up. The first furnace in the family whose fire and work are the same
/// cell, and the first anywhere to mark <see cref="CellRole.Damper"/>.
/// </summary>
public class CrucibleFurnaceLayoutTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private static ExBlockDef Def =>
    BlockCrucibleFurnaceCore.Definitions("iiex").Single();

  private static ExBlockDef HearthDef =>
    BlockCrucibleHearth.Definitions("iiex").Single();

  /// <summary>
  /// The furnace stood at <paramref name="side"/>, completing itself from a raised footprint. Nothing
  /// forces <c>StructureComplete</c>: a drawing whose parts cannot be satisfied at some facing shows up
  /// here as a machine that never completes, which is what the four-sided theory is for.
  /// </summary>
  private static (BlockEntityCrucibleFurnace Furnace, StructureRig Rig) Stood(
    string side
  ) {
    var furnace = new BlockEntityCrucibleFurnace();
    StructureRig rig = Stand(
      furnace,
      Def,
      Anchor,
      "iiex:furnace-cruciblecore-tier1",
      side
    );
    return (furnace, rig);
  }

  #endregion

  #region The drawing

  /// <summary>
  /// One fire cell, and it is the hearth. Every other furnace in the family burns beside its work; here
  /// the coke is packed round the pots, so the drawing marks the single cell they share.
  /// </summary>
  [Fact]
  public void The_fire_and_the_work_are_one_cell() {
    Assert.Equal([new Vec3i(-2, 0, 0)], RoleCellsOf(Def, CellRole.Firebox));
  }

  /// <summary>
  /// The flue runs straight up out of the hearth, and the highest course is the base of the player's
  /// chimney rather than the top of the furnace. Two cells: a machine that drew its whole stack would be
  /// declaring a height the design leaves to the player.
  /// </summary>
  [Fact]
  public void The_flue_is_a_column_over_the_hearth_ending_at_the_stack_base() {
    var flue = RoleCellsOf(Def, CellRole.Flue).OrderBy(c => c.Y).ToList();

    Assert.Equal([new Vec3i(-2, 2, 0), new Vec3i(-2, 3, 0)], flue);
    Assert.All(flue, c => Assert.Equal(new Vec3i(-2, c.Y, 0), c));
  }

  /// <summary>
  /// The damper sits at the foot of the stack, between the hearth and the flue - the only place a layout
  /// can draw one when everything above it is the player's to build. The puddling furnace caps its stack
  /// at the top because its chimney is fixed; this is the same mechanic at the other end.
  /// </summary>
  [Fact]
  public void The_damper_is_at_the_foot_of_the_stack_not_its_top() {
    var damper = RoleCellsOf(Def, CellRole.Damper);
    var flue = RoleCellsOf(Def, CellRole.Flue);

    Assert.Equal([new Vec3i(-2, 1, 0)], damper);
    Assert.All(flue, c => Assert.True(c.Y > damper[0].Y));
    Assert.Equal(RoleCellsOf(Def, CellRole.Firebox)[0].Y + 1, damper[0].Y);
  }

  /// <summary>
  /// Exactly these three roles. Stated as the whole set rather than as three presences, so a fourth role
  /// added by a later edit - a tuyere on a furnace that must never be blown, a pool on one that holds no
  /// bath - fails here rather than quietly changing what the branch reads.
  /// </summary>
  [Fact]
  public void The_drawing_marks_exactly_firebox_flue_and_damper() {
    Assert.Equal(["Firebox", "Flue", "Damper"], RoleNamesOf(Def));
  }

  #endregion

  #region Standing it up

  /// <summary>
  /// It completes at every facing, from a raised footprint and without being told to. A drawing whose
  /// door, shoulders or damper cannot be satisfied at some rotation fails only here.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_furnace_completes_at_every_facing(string side) {
    Assert.True(Stood(side).Furnace.StructureComplete);
  }

  /// <summary>
  /// The hearth resolves as the furnace's own part at every facing, which is what the melt cycle will read
  /// the pots off. The cell is found by role rather than by a copied offset, so a drawing that moved the
  /// hearth would still be picked up.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_hearth_resolves_as_the_furnaces_own_part(string side) {
    (BlockEntityCrucibleFurnace furnace, StructureRig rig) = Stood(side);
    SeatCrucibleHearth(rig, furnace, unitsPerCell: 12);

    Assert.NotNull(furnace.Hearth);
    Assert.Equal(
      furnace.Hearth,
      furnace
        .FireboxCells.Select(c =>
          rig.World.World.BlockAccessor.GetBlockEntity(c)
        )
        .Single()
    );
  }

  /// <summary>
  /// The work door is the cell the drawing puts a charge door in, and not the one the branch defaults to.
  /// The default is the offset both reverberatory drawings agree on; on this drawing that cell is brick,
  /// so an inherited door would resolve to nothing and the furnace would report no venting with its mouth
  /// standing wide open.
  /// </summary>
  [Fact]
  public void The_work_door_is_where_the_drawing_puts_one() {
    var door = (Vec3i?)
      ReflectionHelpers.GetProperty(
        new BlockEntityCrucibleFurnace(),
        "DoorCell"
      );

    Assert.NotNull(door);
    Assert.Contains("chargedoor", At(LayoutOf(Def), door!));
  }

  #endregion

  #region The hearth blocktype

  /// <summary>
  /// The hearth carries the fuel bed behaviour, which is what makes the coke round the pots the same
  /// substrate every other machine in the suite burns - charged, drawn down and retextured by code that
  /// already exists rather than a second implementation of a fuel bed.
  /// </summary>
  [Fact]
  public void The_hearth_is_a_fuel_bed() {
    var behaviours = HearthDef.ToJson()["entityBehaviors"];

    Assert.NotNull(behaviours);
    Assert.Contains(nameof(BEBehaviorFirebox), behaviours!.ToString());
  }

  /// <summary>
  /// The hearth wears whatever refractory it was built from, where the drawn shape names tier3 outright.
  /// Any tier is allowed here, so a hearth built of tier1 brick that rendered as tier3 would be the block
  /// lying about what it cost.
  /// </summary>
  [Fact]
  public void The_hearth_wears_the_tier_it_was_built_from() {
    Assert.Equal(
      "game:block/clay/refractory/{tier}/front1",
      (string?)HearthDef.ToJson()["textures"]!["front1"]!["base"]
    );
    Assert.Equal(
      ["tier1", "tier2", "tier3"],
      HearthDef.ToJson()["variantgroups"]!.First(g =>
        (string?)g["code"] == "tier"
      )["states"]!
        .Select(s => (string)s!)
        .ToArray()
    );
  }

  #endregion

  #region The four holes

  /// <summary>
  /// Four holes, and the element names are the shape's own. Selective-element matching drops a name it
  /// does not know without raising anything, so a group renamed in the editable would surface as a pot
  /// that silently stops drawing rather than as an error.
  /// </summary>
  [Fact]
  public void Every_hole_names_the_elements_the_shape_declares() {
    Assert.Equal(4, CrucibleHearthLayout.Holes);
    Assert.Equal(CrucibleHearthLayout.Holes, CrucibleHearthLayout.All.Count);

    var names = ShapeElementNames();
    foreach (CrucibleHearthLayout.Hole hole in CrucibleHearthLayout.All) {
      Assert.Contains(CrucibleHearthLayout.Pot(hole), names);
      Assert.Contains(CrucibleHearthLayout.Charge(hole), names);
      Assert.Contains(CrucibleHearthLayout.Slag(hole), names);
      Assert.Contains(CrucibleHearthLayout.Cover(hole), names);
    }
  }

  /// <summary>
  /// An empty hearth draws its structure and its bed and nothing else; a loaded hole adds one element per
  /// thing standing in it. The bed comes from the firebox's own list, so both machines' beds are one
  /// selector rather than two copies that can drift apart.
  /// </summary>
  [Fact]
  public void A_hole_draws_one_element_for_each_thing_in_it() {
    var empty = new CrucibleHearthLayout.HoleContents[4];
    string[] bare = CrucibleHearthLayout.ElementsFor(empty, null, 6);

    Assert.Equal(BlockFirebox.ElementsFor(null, 6), bare);

    var loaded = new CrucibleHearthLayout.HoleContents[4];
    loaded[0] = new(Pot: true, Charge: true, Slag: true, Cover: true);
    loaded[2] = new(Pot: true, Charge: false, Slag: false, Cover: false);

    string[] drawn = CrucibleHearthLayout.ElementsFor(loaded, null, 6);

    Assert.Equal(bare.Length + 5, drawn.Length);
    Assert.Contains(
      CrucibleHearthLayout.Cover(CrucibleHearthLayout.Hole.NorthWest),
      drawn
    );
    Assert.DoesNotContain(
      CrucibleHearthLayout.Cover(CrucibleHearthLayout.Hole.SouthEast),
      drawn
    );
  }

  /// <summary>
  /// A hearth's own bed names its courses, not always the shipped default. Threaded through explicitly
  /// (<see cref="BlockEntityCrucibleHearth.RenderElements"/> passes its live <c>Bed</c>) rather than
  /// defaulted, so a hearth declaring its own <c>bedElement</c>/<c>layerPrefix</c> draws those names
  /// instead of silently falling back to <c>Coke/CokeLn</c>, which it does not have.
  /// </summary>
  [Fact]
  public void The_bed_courses_come_from_the_bed_actually_handed_in() {
    var world = new TestWorld();
    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );
    bed.Initialize(
      world.Api,
      new JsonObject(
        new JObject { ["bedElement"] = "CoalLayers", ["layerPrefix"] = "L" }
      )
    );

    var empty = new CrucibleHearthLayout.HoleContents[4];
    string[] drawn = CrucibleHearthLayout.ElementsFor(empty, bed, 2);

    Assert.Contains("CoalLayers/L1", drawn);
    Assert.Contains("CoalLayers/L2", drawn);
    Assert.DoesNotContain("Coke/CokeL1", drawn);
  }

  private static System.Collections.Generic.HashSet<string> ShapeElementNames() {
    var json = Newtonsoft.Json.Linq.JObject.Parse(
      System.IO.File.ReadAllText(
        DefinitionGoldens.SolutionRelative(
          "assets/iiex/shapes/furnace/cruciblehearth.json"
        )
      )
    );
    var names = new System.Collections.Generic.HashSet<string>();

    void Walk(Newtonsoft.Json.Linq.JToken? elements, string prefix) {
      foreach (
        Newtonsoft.Json.Linq.JToken e in elements
          ?? new Newtonsoft.Json.Linq.JArray()
      ) {
        string name = prefix + (string)e["name"]!;
        names.Add(name);
        Walk(e["children"], name + "/");
      }
    }
    Walk(json["elements"], "");
    return names;
  }

  #endregion
}
