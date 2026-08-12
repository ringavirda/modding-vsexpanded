using System;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// Mechanical-power producer for the MP-generator sub-machine. Runs as a constant-power source off
/// the engine's <see cref="BlockEntityEngine.MpPowerBudget"/>, so the network settles at
/// <c>speed = budget / load</c>: rated speed at rated load, slower under heavier loads. In north
/// orientation the axle couples on the north and south faces. Body render and per-axis rotation
/// sense come from <see cref="BEBehaviorMPSubmachineBase"/>.
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorEngineMPGenerator(BlockEntity blockentity)
  : BEBehaviorMPSubmachineBase(blockentity) {
  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);

    // The generator couples on both ends of its axis; the base seeds only the single
    // OutFacingForNetworkDiscovery face, so the opposite connector is wired here.
    if (api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
      tryConnect(OutFacingForNetworkDiscovery.Opposite);
  }

  /// <summary>
  /// Re-applies the axle orientation after a side-variant change. The engine snaps the generator with
  /// <c>ExchangeBlock</c>, which keeps this behavior alive so <see cref="Initialize"/> never re-runs;
  /// this re-seeds the connectors onto the new axis. The body mesh needs no hand-off: it is cached
  /// against the block code, and the exchange has already given this behavior a different one.
  /// </summary>
  public void OnOrientationChanged() {
    SetOrientations();
    if (Api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null) {
      tryConnect(OutFacingForNetworkDiscovery);
      tryConnect(OutFacingForNetworkDiscovery.Opposite);
    }
    Blockentity.MarkDirty(true);
  }

  public override float GetResistance() => 0.0005f;

  public override float GetTorque(long tick, float speed, out float resistance) {
    resistance = 0f;
    var engine = (Blockentity as BlockEntityEngineMPGenerator)?.Engine;
    float budget = engine?.MpPowerBudget ?? 0f;
    if (budget <= 0f)
      return 0f;

    // Constant-power source: torque = budget / speed, so the network settles at speed = budget /
    // load. The divisor is clamped so spin-up asks bounded torque. Torque stays positive; direction
    // comes from the discovery seed and AxisSign.
    float ratedSpeed = LpexValues.MpRatedSpeed;
    float torque = budget / Math.Max(speed, 0.25f * ratedSpeed);

    // Soft top-speed cap: torque tapers to zero between rated speed and 1.5x rated, so a light load
    // settles just above rated without the oscillation a hard cap produces.
    float capEnd = 1.5f * ratedSpeed;
    if (speed >= capEnd)
      return 0f;
    if (speed > ratedSpeed)
      torque *= (capEnd - speed) / (capEnd - ratedSpeed);
    return torque;
  }

  protected override BlockFacing ResolveDiscoveryFace() =>
    // Discovery is seeded from the back of the axis (south/west). The discovery direction drives
    // vanilla's IsRotationReversed, so seeding from the far end makes the shaft's rendered spin
    // match the engine's beam linkage.
    Block.Variant["side"] switch {
      "north" or "south" => BlockFacing.SOUTH,
      "east" or "west" => BlockFacing.WEST,
      _ => BlockFacing.SOUTH,
    };
}
