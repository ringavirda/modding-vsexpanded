using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using IronworkingExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The transmission couples two mpenergy runs across a gear <b>without merging them</b>: it is a machine (not a
/// graph node), reading the south (input) and north (output) networks via <c>GetNetworkAt</c> on its port cells
/// and projecting them onto the reduction constraint. These pin the wiring - the two sides stay separate, the
/// port cells resolve, the north side is held at <c>ω_south / ratio</c>, and a disengaged clutch transfers
/// nothing. The projection maths itself lives in <c>MpEnergyNetworkStateTests</c>.
/// </summary>
public class TransmissionTests
{
  private static BlockTransmission TxBlock(string type, string side)
  {
    var block = TestBlocks.Configure(
      new BlockTransmission(),
      $"iwex:mpenergy-transmission-{type}-{side}",
      700,
      // `type` names the family member; the gearing lives in `kind` (CodePrefixCollision).
      ("type", "transmission"),
      ("kind", type),
      ("side", side)
    );
    return block;
  }

  private static BlockCastIronShaft ShaftBlock(string orientation)
  {
    var block = TestBlocks.Configure(
      new BlockCastIronShaft(),
      $"iwex:mpenergy-shaft-{orientation}",
      701,
      ("type", "shaft"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "shaft");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }

  // Places a transmission of the given type at the origin with a shaft run on each port; returns the world, the
  // BE, and the two networks (already ticked so each carries state).
  private static (
    TestWorld world,
    BlockEntityTransmission be,
    MpEnergyNetwork south,
    MpEnergyNetwork north
  ) Scene(string type)
  {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", sys => new MpEnergyNetwork(sys));

    var block = TxBlock(type, "north");
    var be = new BlockEntityTransmission { Block = block };
    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, block, be);
    world.Attach(be);
    ReflectionHelpers.SetField(be, "_networks", world.Networks);

    int angle = block.StructureAngle;
    BlockPos southPort = ExOrientation.GlobalPos(pos, 0, 0, 1, angle);
    BlockPos northPort = ExOrientation.GlobalPos(pos, 0, 0, -1, angle);
    world.Place(southPort, ShaftBlock("ns"), new BlockEntityCastIronShaft());
    world.Place(northPort, ShaftBlock("ns"), new BlockEntityCastIronShaft());
    world.AddNode(southPort, "mpenergy");
    world.AddNode(northPort, "mpenergy");
    world.Tick(); // build each run's reservoir state off its shaft inertia

    return (
      world,
      be,
      (MpEnergyNetwork)world.NetworkAt(southPort)!,
      (MpEnergyNetwork)world.NetworkAt(northPort)!
    );
  }

  [Fact]
  public void The_two_sides_are_separate_networks()
  {
    var (_, _, south, north) = Scene("x2");
    // The transmission is not a graph node, so the shaft runs on either side never merge into one pool.
    Assert.NotSame(south, north);
  }

  [Fact]
  public void Coupling_holds_the_north_side_at_the_reduced_speed()
  {
    var (_, be, south, north) = Scene("x2");
    south.State!.Speed = 2f;
    south.State.StoredEnergy = MpEnergyNetworkState.EnergyAtSpeed(
      south.State.Inertia,
      2f
    );

    Assert.True(be.TryCouple(0f)); // lossless
    Assert.True(north.State!.Speed > 0f, "the south run drives the north side up through the gear");
    Assert.Equal(south.State.Speed / 2f, north.State.Speed, 3); // x2 reduction
  }

  [Fact]
  public void A_disengaged_clutch_transfers_nothing()
  {
    var (_, be, south, north) = Scene("clutch"); // clutch defaults disengaged
    south.State!.Speed = 2f;

    Assert.False(be.TryCouple(0f));
    Assert.Equal(0f, north.State!.Speed, 4); // the north run stays as it was
  }

  [Fact]
  public void Throwing_the_lever_engages_and_re_engages_the_clutch()
  {
    var (_, be, _, _) = Scene("clutch");
    Assert.False(be.IsEngaged);

    Assert.True(be.ToggleEngaged());
    Assert.True(be.IsEngaged);

    Assert.True(be.ToggleEngaged());
    Assert.False(be.IsEngaged);
  }

  [Fact]
  public void Engaging_the_clutch_starts_the_transfer()
  {
    var (_, be, south, north) = Scene("clutch");
    south.State!.Speed = 1f;

    Assert.False(be.TryCouple(0f)); // disengaged: nothing crosses
    be.ToggleEngaged();

    Assert.True(be.TryCouple(0f)); // engaged: the run now couples 1:1
    Assert.True(north.State!.Speed > 0f);
    Assert.Equal(south.State.Speed, north.State.Speed, 3); // clutch is ratio 1
  }

  [Fact]
  public void A_ratio_block_ignores_the_clutch_toggle()
  {
    var (_, be, _, _) = Scene("x2"); // not a clutch
    Assert.False(be.ToggleEngaged());
    Assert.False(be.IsEngaged);
  }

  [Fact]
  public void The_lever_cell_is_the_footprint_quarter_block()
  {
    var (_, be, _, _) = Scene("clutch");
    int angle = ((BlockTransmission)be.Block).StructureAngle;
    BlockPos lever = ExpandedLib.Helpers.ExOrientation.GlobalPos(be.Pos, 1, 1, 0, angle);

    Assert.True(be.IsLeverCell(lever));
    Assert.False(be.IsLeverCell(be.Pos)); // the principal is not the lever
  }

  #region Animation sync (the coupler has no network broadcast of its own)

  [Fact]
  public void A_meaningful_speed_change_syncs_and_a_negligible_one_does_not()
  {
    var (_, be, _, _) = Scene("x2");
    float step = 0.02f * ExpandedLib.ExlibValues.MpMaxSpeed;

    Assert.True(be.SyncSideSpeeds(step * 2f, step)); // first real reading: push it
    Assert.False(be.SyncSideSpeeds(step * 2.1f, step)); // jitter under the step: no packet
    Assert.True(be.SyncSideSpeeds(step * 5f, step)); // a real change: push it
  }

  [Fact]
  public void Coming_to_a_stop_always_syncs_even_if_the_step_is_small()
  {
    var (_, be, _, _) = Scene("x2");
    float tiny = 0.02f * ExpandedLib.ExlibValues.MpMaxSpeed * 0.1f;
    be.SyncSideSpeeds(tiny, 0f);

    // Below the step threshold, but a shaft stopping must reach the client or it animates forever.
    Assert.True(be.SyncSideSpeeds(0f, 0f));
  }

  #endregion
}
