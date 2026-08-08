using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// The pipe-network vent strategy: a vanilla chimney capping the top connector of an
/// <see cref="IChimneyVentable"/> node (a content mod's passthrough / outlet fitting) draws gas out of
/// the run (a sink, not a leak) and puffs smoke. Matched by the marker interface rather than concrete
/// block types, so this strategy - and the exlib pipe-network core it plugs into - stay free of any
/// content mod's specific fitting classes. The per-chimney draw rate (L/s) is supplied by the mod that
/// registers the "pipe" network, read live from its config. A fresh instance is created per network by
/// the pipe factory, so the fire-sound throttle map is per-network.
/// </summary>
public sealed class ChimneyVent : IPipeVentStrategy
{
  // Last fire-loop start time (world ms) per drawing chimney, so the continuous draught sound
  // restarts seamlessly (just under the 9.26 s clip) instead of stacking every tick.
  private const long ChimneyFireLoopMs = 9000;
  private readonly Dictionary<BlockPos, long> _chimneyFireMs = new();

  private readonly Func<float> _drawRatePerChimney;

  /// <param name="drawRatePerChimney">Gas (L/s) one chimney draws from the network - read live from
  /// the owning mod's config so a retune applies without reconstructing networks.</param>
  public ChimneyVent(Func<float> drawRatePerChimney) =>
    _drawRatePerChimney = drawRatePerChimney;

  /// <summary>
  /// True when <paramref name="block"/> is a chimney. A code-path substring match is deliberate:
  /// vanilla chimney blocks can't be re-attributed without a JSON patch, so sniffing the path is
  /// the only patch-free way to accept vanilla and modded chimney variants alike. Shared by the
  /// vent classification here and lpex's chimney info patch so the two never disagree.
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
  )
  {
    // Only nodes that opt into chimney venting (IChimneyVentable), drawn on their top by a chimney.
    if (
      node is IChimneyVentable
      && face == BlockFacing.UP
      && IsChimney(neighbour)
    )
    {
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
  )
  {
    // Chimney draw (gas only) - drawRate L/s per chimney-capped top connector. Each drawing chimney
    // puffs smoke so the venting is visible.
    float vented = 0f;
    if (!liquid && vents.Count > 0 && state.Volume > 0)
    {
      vented = Math.Min(state.Volume, vents.Count * _drawRatePerChimney());
      state.Volume -= vented;

      foreach (BlockPos chimneyPos in vents)
      {
        if (manager.ServerWorld is { } smokeWorld)
          ExParticles.ChimneySmoke(smokeWorld, chimneyPos, state.MediumType);
        // A continuous low fire roar marks the chimney pulling the network's draught.
        if (manager.ServerWorld is { } w)
        {
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

    // Drop sound-throttle stamps for chimneys no longer venting this network, so the map can't grow
    // without bound as chimneys are added and removed over a long uptime.
    if (_chimneyFireMs.Count > vents.Count)
    {
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
