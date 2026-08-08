using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The mechanical-energy network end to end, with the <b>real</b> mill block entity on a <b>real</b> network.
/// This is the increment that mattered: before it the whole chain (flywheel → shaft run → transmissions) was a
/// closed loop that spun up to ω_max and nothing ever spent, because <c>LoadTorque</c> was hard-coded to zero.
/// <para>
/// What these pin is that the design's headline couplings are <b>emergent from the torque balance</b>, not
/// special cases: a run either carries a pass or is wound to a hard stall (there is no "buffer up and fire"),
/// cold stock is refused before it can ever load the line, and — the flywheel's whole reason to exist — a
/// heavy pass that stalls a small wheel is carried through by a big one.
/// </para>
/// </summary>
public class RollingMillLoadTests
{
  // A drive we can throttle, standing in for the flywheel's vanilla-MP bridge (one water wheel = 1 N·m).
  private sealed class DriveNode : BlockEntity, IMpEnergyProducer
  {
    public float Torque { get; set; }

    public float DriveTorque(float speed) => Torque;
  }

  // The flywheel's inertia is the dial under test here, so a plain storage node stands in for the real block
  // entity (which reads its inertia from its variant + config). The mill is the real thing.
  private sealed class FlywheelNode : BlockEntity, IMpEnergyStorage
  {
    public float Inertia { get; init; }
  }

  private const float NormalWheel = 10f; // IwexValues.FlywheelInertiaNormal
  private const float LargeWheel = 150f; // IwexValues.FlywheelInertiaLarge

  private static (TestWorld World, MpEnergyNetwork Net, BlockEntityRollingMill Mill, DriveNode Drive) Run(
    float inertia = NormalWheel,
    float driveTorque = 1f // FlywheelBridgeChargePower: one water wheel through the bridge
  )
  {
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
    foreach (var (pos, be) in cells)
    {
      world.Place(pos, TestBlocks.Configure(new Block(), $"iwex:millrun{id}", id), be);
      net.Nodes.Add(pos);
      id++;
    }
    return (world, net, mill, drive);
  }

  private static void Spin(MpEnergyNetwork net, TestWorld world, int ticks)
  {
    for (int i = 0; i < ticks; i++)
      net.OnTick(world.Accessor, 1f, world.Networks);
  }

  /// <summary>Spins the line up to its steady state before any load - the startup ritual a heavy wheel needs.</summary>
  private static float Charge(MpEnergyNetwork net, TestWorld world)
  {
    Spin(net, world, 2000);
    return net.State!.Speed;
  }

  /// <summary>Runs the line and the pass together, the way the mill's own tick listener does in game. Returns
  /// whether the pass got through.</summary>
  private static bool Work(
    MpEnergyNetwork net,
    TestWorld world,
    BlockEntityRollingMill mill,
    int seconds
  )
  {
    for (int i = 0; i < seconds; i++)
    {
      net.OnTick(world.Accessor, 1f, world.Networks);
      if (mill.AdvancePass(1f, net.State!.Speed))
        return true;
    }
    return false;
  }

  // A pass one water wheel is meant to carry: narrow, shallow, at rolling heat.
  private static bool LightHotPass(BlockEntityRollingMill mill, float length = 40f) =>
    mill.BeginPass(draft: 0.5f, width: 4f, length: length, tempC: 1100f);

  // Wide stock at the deepest draft the rolls can bite - a plate-mill pass, and more than one wheel can hold.
  private static bool HeavyHotPass(BlockEntityRollingMill mill, float length = 200f) =>
    mill.BeginPass(draft: 1f, width: 16f, length: length, tempC: 1100f);

  [Fact]
  public void A_pass_is_demand_the_network_can_actually_see()
  {
    // The headline. Before this increment LoadTorque was hard-coded to 0, so DemandPower could never be
    // anything but zero however much machinery you built.
    var (world, net, mill, _) = Run();
    Charge(net, world);
    Assert.Equal(0f, net.State!.DemandPower, 4);

    Assert.True(LightHotPass(mill));
    Spin(net, world, 1);
    Assert.True(net.State!.DemandPower > 0f, "the run should now be feeling the mill");

    mill.CancelPass();
    Spin(net, world, 1);
    Assert.Equal(0f, net.State!.DemandPower, 4);
  }

  [Fact]
  public void One_water_wheel_carries_a_light_hot_pass_indefinitely()
  {
    var (world, net, mill, _) = Run();
    float free = Charge(net, world);
    Assert.True(free > 0f);

    Assert.True(LightHotPass(mill));
    Spin(net, world, 100);

    // The load fits inside the drive's headroom, so the line holds its speed and the pass just runs.
    Assert.Equal(free, net.State!.Speed, 3);
  }

  [Fact]
  public void A_run_either_carries_the_pass_or_is_wound_to_a_hard_stall()
  {
    // There is no middle "sag and struggle on" equilibrium, and that is deliberate: the standing resistance
    // means a drive that cannot beat load + friction has its shaft wound all the way down. This is the
    // design's "not a battery" rule - you cannot accumulate your way through an over-heavy pass.
    var (world, net, mill, _) = Run();
    Charge(net, world);

    Assert.True(HeavyHotPass(mill));
    Spin(net, world, 400);

    Assert.Equal(0f, net.State!.Speed, 3);
  }

  [Fact]
  public void A_big_flywheel_carries_a_pass_that_stalls_a_small_one()
  {
    // The flywheel's entire reason to exist: the drive alone cannot hold this pass either way, but stored
    // inertia is dumped into the bite and a large wheel gets the piece out before the line winds down.
    var (smallWorld, smallNet, smallMill, _) = Run(inertia: NormalWheel);
    var (largeWorld, largeNet, largeMill, _) = Run(inertia: LargeWheel);
    Charge(smallNet, smallWorld);
    Charge(largeNet, largeWorld);

    Assert.True(HeavyHotPass(smallMill));
    Assert.True(HeavyHotPass(largeMill));

    Assert.False(Work(smallNet, smallWorld, smallMill, 200), "the small wheel should bog down");
    Assert.True(Work(largeNet, largeWorld, largeMill, 200), "the large wheel should carry it through");
  }

  [Fact]
  public void Cold_stock_never_reaches_the_stall_because_it_is_refused_at_the_bite()
  {
    // Worth stating outright, because it is natural to assume cold stock stalls the line: it never gets that
    // far. delta_max = mu^2 R collapses with the friction, so the rolls cannot pull a cold piece in at all.
    // Cold rolling - shallow drafts at huge force - is the later elex-era machine.
    var (world, net, mill, _) = Run();
    Charge(net, world);

    Assert.False(mill.BeginPass(draft: 0.5f, width: 4f, length: 40f, tempC: 899f));
    Assert.False(mill.IsRolling);
    Spin(net, world, 1);
    Assert.Equal(0f, net.State!.DemandPower, 4); // the line never even felt it
  }

  [Fact]
  public void A_stalled_line_leaves_the_piece_stuck_mid_bite_and_resumes_on_more_power()
  {
    var (world, net, mill, drive) = Run(inertia: NormalWheel);
    Charge(net, world);
    Assert.True(HeavyHotPass(mill));
    Work(net, world, mill, 30); // stalls on torque well inside the heat window

    float stuckAt = mill.Remaining;
    Assert.True(mill.IsRolling, "the pass is jammed, not lost");
    mill.AdvancePass(1f, net.State!.Speed);
    Assert.Equal(stuckAt, mill.Remaining, 4);
    Assert.True(mill.IsStalled);

    // Put a real engine on it and the same piece moves again, from exactly where it jammed.
    drive.Torque = 20f;
    Spin(net, world, 20);
    mill.AdvancePass(1f, net.State!.Speed);
    Assert.True(mill.Remaining < stuckAt);
    Assert.False(mill.IsStalled);
  }

  [Fact]
  public void The_line_recovers_its_speed_once_the_stock_clears_the_rolls()
  {
    var (world, net, mill, _) = Run();
    float free = Charge(net, world);

    Assert.True(HeavyHotPass(mill));
    Spin(net, world, 100);
    Assert.True(net.State!.Speed < free);

    mill.CancelPass();
    Charge(net, world);
    Assert.Equal(free, net.State!.Speed, 3);
  }

  [Fact]
  public void A_stopped_run_cannot_draw_the_stock_through_at_all()
  {
    // The startup ritual: with no drive the shaft never turns, so the bite never advances however long you
    // wait. Emergent - AdvancePass simply multiplies by omega.
    var (world, net, mill, _) = Run(driveTorque: 0f);
    Spin(net, world, 20);
    Assert.Equal(0f, net.State!.Speed, 4);

    Assert.True(LightHotPass(mill, length: 4f));
    float before = mill.Remaining;
    Assert.False(Work(net, world, mill, 20));

    Assert.Equal(before, mill.Remaining, 4);
    Assert.True(mill.IsStalled);
  }

  [Fact]
  public void A_jam_is_self_worsening_because_the_stuck_piece_keeps_cooling()
  {
    // The piece cools whether or not it is moving, so a stall compounds: what began as a pass one wheel could
    // carry becomes one it cannot, and past the bite threshold the answer is the reheat furnace rather than
    // more power. This is the keep-it-hot loop closing on itself.
    var (world, net, mill, _) = Run(driveTorque: 0f);
    Assert.True(LightHotPass(mill, length: 4f));
    float atEntry = mill.LoadTorque(1f);

    Work(net, world, mill, 60); // jammed in a dead mill for a minute

    Assert.True(
      mill.LoadTorque(1f) > atEntry,
      "a piece stuck in the rolls should stiffen as it loses heat"
    );
  }

  [Fact]
  public void Stock_left_waiting_on_a_dead_line_goes_cold_and_stops_being_rollable()
  {
    // The heat budget, at the machine. The fuse is long enough that a piece survives a normal spin-up - so
    // charging first is a convenience, not a requirement - but leave it sitting in a dead mill and it drops
    // below rolling heat, at which point the pass freezes and no amount of power restarts it. The way out is
    // the wrench and the reheat furnace, not a bigger engine.
    var (world, net, mill, drive) = Run(driveTorque: 0f);
    Assert.True(LightHotPass(mill, length: 4f));

    Work(net, world, mill, 400); // a long wait on a dead line
    drive.Torque = 20f; // then all the power in the world
    Spin(net, world, 50);

    Assert.False(Work(net, world, mill, 100), "a cold piece should stay frozen however hard it is driven");
    Assert.True(mill.IsStalled);
  }
}
