using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The mechanical-energy network end to end, with the real mill block entity on a real network. The
/// couplings covered here fall out of the torque balance rather than being special-cased: a run either
/// carries a pass or is wound to a hard stall, cold stock is refused before it can load the line, and a
/// pass a weak drive cannot hold is carried through by a large enough flywheel.
/// <para>
/// A working stand draws a declared demand, so what makes a pass heavy here is the run - how much drive it
/// has and how much it stored - and never the piece. The one thing about the stock that still moves the
/// load is its heat, which is the mill's own thesis.
/// </para>
/// See docs/design/mechanics/mp-energy.md.
/// </summary>
public class RollingMillLoadTests {
  // A throttleable drive standing in for the flywheel's vanilla-MP bridge (one water wheel = 1 N.m).
  private sealed class DriveNode : BlockEntity, IMpEnergyProducer {
    public float Torque { get; set; }

    public float DriveTorque(float speed) => Torque;
  }

  // Inertia is the dial under test, so a plain storage node stands in for the real flywheel block entity,
  // which reads its inertia from its variant and config. The mill itself is the production type.
  private sealed class FlywheelNode : BlockEntity, IMpEnergyStorage {
    public float Inertia { get; init; }
  }

  private const float NormalWheel = 10f; // IwexValues.FlywheelInertiaNormal
  private const float LargeWheel = 150f; // IwexValues.FlywheelInertiaLarge

  private static (
    TestWorld World,
    MpEnergyNetwork Net,
    BlockEntityRollingMill Mill,
    DriveNode Drive
  ) Run(
    float inertia = NormalWheel,
    float driveTorque = 1f // FlywheelBridgeChargePower: one water wheel through the bridge
  ) {
    var world = new TestWorld();
    var net = new MpEnergyNetwork(world.Networks);

    var drive = new DriveNode { Torque = driveTorque };
    var mill = new BlockEntityRollingMill();

    var cells = new (BlockPos Pos, BlockEntity Be)[]
    {
      (new BlockPos(0, 0, 0), new FlywheelNode { Inertia = inertia }),
      (new BlockPos(1, 0, 0), drive),
      (new BlockPos(2, 0, 0), mill),
    };
    int id = 900;
    foreach (var (pos, be) in cells) {
      world.Place(
        pos,
        TestBlocks.Configure(new Block(), $"iwex:millrun{id}", id),
        be
      );
      net.Nodes.Add(pos);
      id++;
    }
    return (world, net, mill, drive);
  }

  private static void Spin(MpEnergyNetwork net, TestWorld world, int ticks) {
    for (int i = 0; i < ticks; i++)
      net.OnTick(world.Accessor, 1f, world.Networks);
  }

  /// <summary>Spins the line up to its unloaded steady state and returns that speed.</summary>
  private static float Charge(MpEnergyNetwork net, TestWorld world) {
    Spin(net, world, 2000);
    return net.State!.Speed;
  }

  /// <summary>Runs the line and the pass together, the way the mill's own tick listener does in game.
  /// Returns whether the pass cleared the rolls.</summary>
  private static bool Work(
    MpEnergyNetwork net,
    TestWorld world,
    BlockEntityRollingMill mill,
    int seconds
  ) {
    for (int i = 0; i < seconds; i++) {
      net.OnTick(world.Accessor, 1f, world.Networks);
      if (mill.AdvancePass(1f, net.State!.Speed))
        return true;
    }
    return false;
  }

  // A drive that cannot hold a working stand: 0.7 N.m against 0.34 of declared load and the network's 0.5
  // standing floor. There is no equilibrium below it, so the shaft winds all the way down.
  private const float WeakDrive = 0.7f;

  // The pass, one bar's ordinary round at rolling heat. Its draft and length no longer reach the load -
  // the stand's demand is declared - so the same call stands in for every pass these tests run.
  private static bool HotPass(
    BlockEntityRollingMill mill,
    float length = 40f
  ) => mill.BeginPass(draft: 0.25f, length: length, tempC: 1100f);

  [Fact]
  public void A_pass_is_demand_the_network_can_actually_see() {
    // A pass in the rolls is what puts DemandPower above zero; cancelling it takes the demand back off.
    var (world, net, mill, _) = Run();
    Charge(net, world);
    Assert.Equal(0f, net.State!.DemandPower, 4);

    Assert.True(HotPass(mill));
    Spin(net, world, 1);
    Assert.True(
      net.State!.DemandPower > 0f,
      "the run should now be feeling the mill"
    );

    mill.CancelPass();
    Spin(net, world, 1);
    Assert.Equal(0f, net.State!.DemandPower, 4);
  }

  [Fact]
  public void One_water_wheel_carries_a_hot_pass_indefinitely() {
    var (world, net, mill, _) = Run();
    float free = Charge(net, world);
    Assert.True(free > 0f);

    Assert.True(HotPass(mill));
    Spin(net, world, 100);

    // The demand is calibrated to sit inside one bridged wheel's headroom, so the line holds its speed and
    // the pass runs on. That margin is what RollingLoadTorque is chosen against.
    Assert.Equal(free, net.State!.Speed, 3);
  }

  [Fact]
  public void A_run_either_carries_the_pass_or_is_wound_to_a_hard_stall() {
    // There is no intermediate equilibrium: standing resistance means a drive that cannot beat load plus
    // friction has its shaft wound all the way down. Speed cannot be accumulated through a pass the drive
    // cannot hold.
    var (world, net, mill, _) = Run(driveTorque: WeakDrive);
    Charge(net, world);

    Assert.True(HotPass(mill, length: 400f));
    Spin(net, world, 400);

    Assert.Equal(0f, net.State!.Speed, 3);
  }

  [Fact]
  public void A_big_flywheel_carries_a_pass_that_stalls_a_small_one() {
    // Both lines are charged and then lose their drive outright, which is the case the flywheel exists for.
    // What is left is stored inertia spent into the bite, so a large wheel gets the piece out where a small
    // one winds down under it.
    var (smallWorld, smallNet, smallMill, smallDrive) = Run(
      inertia: NormalWheel
    );
    var (largeWorld, largeNet, largeMill, largeDrive) = Run(
      inertia: LargeWheel
    );
    Charge(smallNet, smallWorld);
    Charge(largeNet, largeWorld);
    smallDrive.Torque = 0f;
    largeDrive.Torque = 0f;

    Assert.True(HotPass(smallMill, length: 200f));
    Assert.True(HotPass(largeMill, length: 200f));

    Assert.False(
      Work(smallNet, smallWorld, smallMill, 200),
      "the small wheel should bog down"
    );
    Assert.True(
      Work(largeNet, largeWorld, largeMill, 200),
      "the large wheel should carry it through"
    );
  }

  [Fact]
  public void Cold_stock_never_reaches_the_stall_because_it_is_refused_at_the_bite() {
    // Cold stock never loads the line at all: delta_max = mu^2 R collapses with the friction, so the rolls
    // cannot pull a cold piece in.
    var (world, net, mill, _) = Run();
    Charge(net, world);

    Assert.False(mill.BeginPass(draft: 0.25f, length: 40f, tempC: 899f));
    Assert.False(mill.IsRolling);
    Spin(net, world, 1);
    Assert.Equal(0f, net.State!.DemandPower, 4); // no demand ever reached the line
  }

  [Fact]
  public void A_stalled_line_leaves_the_piece_stuck_mid_bite_and_resumes_on_more_power() {
    var (world, net, mill, drive) = Run(inertia: NormalWheel);
    Charge(net, world);
    drive.Torque = 0f; // the drive drops out with a charged wheel and a piece to draw
    Assert.True(HotPass(mill, length: 200f));
    Work(net, world, mill, 30); // stalls on torque well inside the heat window

    float stuckAt = mill.Remaining;
    Assert.True(mill.IsRolling, "the pass is jammed, not lost");
    mill.AdvancePass(1f, net.State!.Speed);
    Assert.Equal(stuckAt, mill.Remaining, 4);
    Assert.True(mill.IsStalled);

    // More drive torque and the same piece moves again, from where it jammed.
    drive.Torque = 20f;
    Spin(net, world, 20);
    mill.AdvancePass(1f, net.State!.Speed);
    Assert.True(mill.Remaining < stuckAt);
    Assert.False(mill.IsStalled);
  }

  [Fact]
  public void The_line_recovers_its_speed_once_the_stock_clears_the_rolls() {
    var (world, net, mill, _) = Run(driveTorque: WeakDrive);
    float free = Charge(net, world);

    Assert.True(HotPass(mill, length: 400f));
    Spin(net, world, 100);
    Assert.True(net.State!.Speed < free);

    mill.CancelPass();
    Charge(net, world);
    Assert.Equal(free, net.State!.Speed, 3);
  }

  [Fact]
  public void A_stopped_run_cannot_draw_the_stock_through_at_all() {
    // With no drive the shaft never turns and the bite never advances: AdvancePass multiplies by omega.
    var (world, net, mill, _) = Run(driveTorque: 0f);
    Spin(net, world, 20);
    Assert.Equal(0f, net.State!.Speed, 4);

    Assert.True(HotPass(mill, length: 4f));
    float before = mill.Remaining;
    Assert.False(Work(net, world, mill, 20));

    Assert.Equal(before, mill.Remaining, 4);
    Assert.True(mill.IsStalled);
  }

  [Fact]
  public void A_jam_is_self_worsening_because_the_stuck_piece_keeps_cooling() {
    // The piece cools whether or not it is moving, so a stall compounds: a pass one wheel could carry
    // becomes one it cannot, and past the bite threshold no amount of torque recovers it.
    var (world, net, mill, _) = Run(driveTorque: 0f);
    Assert.True(HotPass(mill, length: 4f));
    float atEntry = mill.LoadTorque(1f);

    Work(net, world, mill, 60); // jammed in a dead mill for a minute

    Assert.True(
      mill.LoadTorque(1f) > atEntry,
      "a piece stuck in the rolls should stiffen as it loses heat"
    );
  }

  [Fact]
  public void Stock_left_waiting_on_a_dead_line_goes_cold_and_stops_being_rollable() {
    // A piece survives a normal spin-up, so charging the line first is a convenience rather than a
    // requirement. Left in a dead mill it drops below rolling heat, and the pass then stays frozen at any
    // drive torque.
    var (world, net, mill, drive) = Run(driveTorque: 0f);
    Assert.True(HotPass(mill, length: 4f));

    Work(net, world, mill, 400); // a long wait on a dead line
    drive.Torque = 20f; // then ample drive torque
    Spin(net, world, 50);

    Assert.False(
      Work(net, world, mill, 100),
      "a cold piece should stay frozen however hard it is driven"
    );
    Assert.True(mill.IsStalled);
  }
}
