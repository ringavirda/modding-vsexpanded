using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using BoilerState = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The Cornish boiler's own fuel bed: the firing gesture that lights it, the burn that draws it down at
/// the charged fuel's own rate, and the port faces the vessel couples on, which are declared once in the
/// north-orientation footprint and have to turn with the machine.
/// </summary>
public class BoilerFireboxTests {
  #region Firing

  [Fact]
  public void A_charged_bed_can_be_lit_only_through_an_open_main_hatch() {
    var rig = new BoilerRig().ExtinguishFire();

    Assert.False(
      rig.Be.CanLightBed,
      "a full bed behind a shut firing door is not ready to light"
    );

    rig.Be.ToggleMainHatch();
    Assert.True(rig.Be.CanLightBed);

    rig.Be.LightBed();
    Assert.True(rig.Lit);
    // Lighting consumes the gesture: the next hold has the door to close, not a fire to light.
    Assert.False(rig.Be.CanLightBed);
  }

  [Fact]
  public void A_part_charged_bed_will_not_take_a_light() {
    var rig = new BoilerRig().ExtinguishFire();
    rig.ChargeBed(BoilerRig.DefaultFuel, units: 1);
    rig.Be.ToggleMainHatch();

    Assert.False(rig.Be.CanLightBed);
    rig.Be.LightBed();
    Assert.False(rig.Lit);
  }

  [Fact]
  public void Charging_takes_fuel_out_of_the_hand_and_refuses_what_does_not_burn() {
    var rig = new BoilerRig();
    rig.Bed.Clear();

    var rock = new DummySlot(
      new ItemStack(rig.World.RegisterItem("game:rock-granite"), 4)
    );
    rig.Be.TryChargeBed(Substitute.For<IPlayer>(), rock);
    Assert.Equal(0, rig.Bed.Units);
    Assert.Equal(4, rock.Itemstack.StackSize);

    var coal = new DummySlot(
      new ItemStack(rig.World.RegisterItem(BoilerRig.DefaultFuel), 4)
    );
    rig.Be.TryChargeBed(Substitute.For<IPlayer>(), coal);
    Assert.Equal(4, rig.Bed.Units);
    Assert.True(coal.Empty, "the charged units leave the hand");
  }

  #endregion

  #region Burn-down

  // Bituminous coal burns 84 s per unit (vanilla's own combustibleProps, mirrored by TestWorld).
  private const float BituminousSecondsPerUnit = 84f;

  [Fact]
  public void A_lit_bed_loses_one_unit_per_the_fuels_own_burn_duration() {
    var rig = new BoilerRig().SetState(BoilerState.Idle);
    int charged = rig.Bed.Units;

    rig.Tick(times: (int)BituminousSecondsPerUnit - 1);
    Assert.Equal(charged, rig.Bed.Units);

    rig.Tick();
    Assert.Equal(charged - 1, rig.Bed.Units);
  }

  [Fact]
  public void An_unlit_bed_is_not_drawn_down() {
    var rig = new BoilerRig().SetState(BoilerState.Idle).ExtinguishFire();
    int charged = rig.Bed.Units;

    rig.Tick(times: (int)BituminousSecondsPerUnit * 2);

    Assert.Equal(charged, rig.Bed.Units);
  }

  [Fact]
  public void The_fire_goes_out_when_the_last_unit_burns_away() {
    var rig = new BoilerRig().SetState(BoilerState.Idle);
    rig.ChargeBed(BoilerRig.DefaultFuel, units: 1);
    rig.RelightFire();

    rig.Tick(times: (int)BituminousSecondsPerUnit);

    Assert.Equal(0, rig.Bed.Units);
    Assert.False(rig.Lit, "an empty bed cannot stay alight");
  }

  #endregion

  #region Port faces

  /// <summary>
  /// A boiler carrying the shipped attributes at <paramref name="side"/>. Only the block is needed:
  /// both faces are read off the definition's own declarations.
  /// </summary>
  private static BlockBoilerCornish BlockAt(string side, int id) {
    var block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      $"iiex:boilercornish-{side[0]}",
      id,
      ("side", side)
    );
    ExBlockDef def = BlockBoilerCornish.Definitions("iiex").Single();
    block.Attributes = new JsonObject((JObject)def.ToJson()["attributes"]!);
    return block;
  }

  // The footprint declares water on the principal's south face and exhaust on the east face of the E
  // cell, both in the north-orientation layout. The vessel's own angle is the side angle plus 180, so
  // a north-placed boiler takes water on its world-north face - and a literal facing left unturned
  // would have every boiler but one reading a neighbour that is not there.
  [Theory]
  [InlineData("north", "north", "west")]
  [InlineData("east", "east", "north")]
  [InlineData("south", "south", "east")]
  [InlineData("west", "west", "south")]
  public void The_port_faces_turn_with_the_vessel(
    string side,
    string feedwater,
    string exhaust
  ) {
    BlockBoilerCornish block = BlockAt(side, 500 + side.Length);

    Assert.Equal(
      ExOrientation.FacingFromSide(feedwater),
      block.FeedwaterWorldFace
    );
    Assert.Equal(ExOrientation.FacingFromSide(exhaust), block.ExhaustWorldFace);
    // The steam port is declared UP, which no rotation moves - but it is read back off the footprint all
    // the same, so re-declaring it on a side face moves where the vessel pushes steam with it.
    Assert.Equal(BlockFacing.UP, block.SteamWorldFace);
    Assert.True(
      block.HasConnectorAt(block.FeedwaterWorldFace),
      "the feedwater face is the one a pipe couples to"
    );
    Assert.False(block.HasConnectorAt(BlockFacing.DOWN));
  }

  #endregion
}
