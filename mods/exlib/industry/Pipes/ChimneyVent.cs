using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Industry.Pipes;

/// <summary>
/// Pipe-network vent strategy: a vanilla chimney capping the top connector of an
/// <see cref="IChimneyVentable"/> node draws gas out of the run and puffs smoke. Nodes are matched by
/// the marker interface rather than concrete block types, so the strategy and the exlib pipe-network
/// core stay free of any content mod's fitting classes. The per-chimney draw rate (L/s) is supplied by
/// the mod that registers the "pipe" network and read live from its config. The pipe factory creates
/// one instance per network, so the fire-sound throttle map is per-network.
/// </summary>
public sealed class ChimneyVent : IPipeVentStrategy {
  // Fire-loop restart interval (ms), just under the 9.26 s clip, so the draught sound is continuous
  // instead of stacking every tick. The map holds the last loop start (world ms) per drawing chimney.
  private const long ChimneyFireLoopMs = 9000;
  private readonly Dictionary<BlockPos, long> _chimneyFireMs = new();

  private readonly Func<float> _drawRatePerChimney;

  /// <param name="drawRatePerChimney">Gas (L/s) one chimney draws from the network - read live from
  /// the owning mod's config so a retune applies without reconstructing networks.</param>
  public ChimneyVent(Func<float> drawRatePerChimney) =>
    _drawRatePerChimney = drawRatePerChimney;

  /// <summary>
  /// True when <paramref name="block"/> is a chimney. Matches on the code path because vanilla chimney
  /// blocks carry no attribute to test without a JSON patch, and the substring accepts modded variants
  /// as well. Shared with iiex's chimney info patch so both classify identically.
  /// </summary>
  public static bool IsChimney(Block? block) =>
    block?.Code?.Path?.Contains("chimney") == true;

  /// <inheritdoc/>
  public bool TryClassifyVent(
    IBlockAccessor blockAccessor,
    BlockNetworkNode node,
    BlockPos pos,
    BlockFacing face,
    Block neighbour,
    out BlockPos ventPos
  ) {
    // Only nodes that opt into chimney venting (IChimneyVentable), drawn on their top by a chimney.
    if (
      node is IChimneyVentable
      && face == BlockFacing.UP
      && IsChimney(neighbour)
    ) {
      ventPos = pos.AddCopy(face);
      return true;
    }
    ventPos = pos;
    return false;
  }

  /// <inheritdoc/>
  public float Vent(
    IReadOnlyList<BlockPos> vents,
    PipeNetworkState state,
    bool liquid,
    BlockNetworkModSystem manager
  ) {
    // Chimney draw (gas only) - drawRate L/s per chimney-capped top connector. Each drawing chimney
    // puffs smoke so the venting is visible.
    float vented = 0f;
    if (!liquid && vents.Count > 0 && state.Volume > 0) {
      vented = Math.Min(state.Volume, vents.Count * _drawRatePerChimney());
      state.Volume -= vented;

      foreach (BlockPos chimneyPos in vents) {
        if (manager.ServerWorld is { } smokeWorld)
          ExParticles.ChimneySmoke(smokeWorld, chimneyPos, state.MediumType);
        // Continuous fire loop while the chimney pulls the network's draught.
        if (manager.ServerWorld is { } w) {
          long last = _chimneyFireMs.GetValueOrDefault(chimneyPos);
          ExSounds.PlayLoop(
            w,
            chimneyPos,
            ExSounds.Fire,
            ref last,
            ChimneyFireLoopMs,
            volume: 0.3f,
            range: 20f
          );
          _chimneyFireMs[chimneyPos] = last;
        }
      }
    }

    // Drop sound-throttle stamps for chimneys no longer venting this network, so the map stays bounded
    // as chimneys are added and removed.
    if (_chimneyFireMs.Count > vents.Count) {
      List<BlockPos>? stale = null;
      foreach (var key in _chimneyFireMs.Keys)
        if (!vents.Contains(key))
          (stale ??= []).Add(key);
      if (stale != null)
        foreach (var key in stale)
          _chimneyFireMs.Remove(key);
    }

    return vented;
  }
}
