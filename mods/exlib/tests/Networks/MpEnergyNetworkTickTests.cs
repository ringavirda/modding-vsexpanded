using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The mechanical-energy network's per-tick loop (<see cref="MpEnergyNetwork.OnTick"/>): it walks the nodes at
/// the current shaft speed, sums the storage inertia, the drive torque and the load torque, then integrates
/// the one shaft. Driven here with node doubles, covering discovery of storage/drives/loads and the rule that
/// torque, not accumulated energy, governs whether the run turns. The shaft arithmetic itself is covered by
/// <see cref="MpEnergyNetworkStateTests"/>.
/// </summary>
public class MpEnergyNetworkTickTests {
  private sealed class StorageNode : BlockEntity, IMpEnergyStorage {
    public float Inertia { get; init; }
  }

  private sealed class DriveNode : BlockEntity, IMpEnergyProducer {
    public float Torque { get; set; }

    public float DriveTorque(float speed) => Torque;
  }

  private sealed class LoadNode : BlockEntity, IMpEnergyConsumer {
    public float Torque { get; set; }

    public float LoadTorque(float speed) => Torque;
  }

  /// <summary>Builds one network holding the given nodes, placed in the world so the tick resolves them by
  /// position. Bypasses the graph so the loop can be driven without connector-correct doubles.</summary>
  private static MpEnergyNetwork Wire(
    TestWorld world,
    params (BlockPos Pos, BlockEntity Be)[] nodes
  ) {
    var net = new MpEnergyNetwork(world.Networks);
    int id = 100;
    foreach (var (pos, be) in nodes) {
      world.Place(
        pos,
        TestBlocks.Configure(new Block(), $"test:mpnode{id}", id),
        be
      );
      net.Nodes.Add(pos);
      id++;
    }
    return net;
  }

  [Fact]
  public void A_drive_spins_the_storage_run_up() {
    var world = new TestWorld();
    var net = Wire(
      world,
      (new BlockPos(0, 0, 0), new StorageNode { Inertia = 10f }),
      (new BlockPos(1, 0, 0), new DriveNode { Torque = 5f })
    );

    net.OnTick(world.Accessor, 1f, world.Networks);

    var s = net.State!;
    Assert.Equal(10f, s.Inertia, 3); // storage inertia summed
    Assert.True(
      s.Speed > 0f,
      "a drive above the idle floor should spin the shaft up"
    );
    Assert.Equal(
      MpEnergyNetworkState.EnergyAtSpeed(10f, s.Speed),
      s.StoredEnergy,
      3
    );
    Assert.Equal(5f * s.Speed, s.SupplyPower, 3); // display power = τ_drive·ω
  }

  [Fact]
  public void A_load_beyond_the_drive_stalls_the_run() {
    var world = new TestWorld();
    var net = Wire(
      world,
      (new BlockPos(0, 0, 0), new StorageNode { Inertia = 10f }),
      (new BlockPos(1, 0, 0), new DriveNode { Torque = 1f }),
      (new BlockPos(2, 0, 0), new LoadNode { Torque = 5f })
    );

    net.OnTick(world.Accessor, 1f, world.Networks);

    // The drive cannot out-torque the load, so the shaft never leaves rest and no energy accumulates.
    Assert.Equal(0f, net.State!.Speed);
    Assert.Equal(0f, net.State!.StoredEnergy);
  }

  [Fact]
  public void A_charged_flywheel_coasts_when_the_drive_is_cut() {
    var world = new TestWorld();
    var drive = new DriveNode { Torque = 50f };
    var net = Wire(
      world,
      (new BlockPos(0, 0, 0), new StorageNode { Inertia = 100f }),
      (new BlockPos(1, 0, 0), drive)
    );

    net.OnTick(world.Accessor, 1f, world.Networks); // spin the heavy wheel up
    float spun = net.State!.Speed;
    Assert.True(spun > 0f);

    drive.Torque = 0f; // steam off
    net.OnTick(world.Accessor, 1f, world.Networks);

    // A large inertia coasts: still turning, slower, rather than stopping the instant the drive does.
    Assert.True(net.State!.Speed > 0f && net.State!.Speed < spun);
  }

  [Fact]
  public void A_run_with_no_storage_has_no_reservoir() {
    var world = new TestWorld();
    // A drive but no inertia: the tick drops the reservoir (nothing to spin).
    var net = Wire(
      world,
      (new BlockPos(0, 0, 0), new DriveNode { Torque = 5f })
    );

    net.OnTick(world.Accessor, 1f, world.Networks);

    Assert.Null(net.State);
  }
}
