using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PipesAndPowerExpanded.BlockNetworkPipe;

/// <summary>
/// ppex's pipe-network vent strategy: a vanilla chimney capping the top connector of a passthrough /
/// passthrough-bend / outlet block draws gas out of the run (a sink, not a leak) and puffs smoke.
/// A fresh instance is created per network by the pipe factory, so the fire-sound throttle map is
/// per-network. Removes the concrete pipe-block, particle and sound knowledge from the (exlib) pipe
/// network core.
/// </summary>
public sealed class PpexChimneyVent : IPipeVentStrategy
{
  // Last fire-loop start time (world ms) per drawing chimney, so the continuous draught sound
  // restarts seamlessly (just under the 9.26 s clip) instead of stacking every tick.
  private const long ChimneyFireLoopMs = 9000;
  private readonly Dictionary<BlockPos, long> _chimneyFireMs = new();

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
    // Only the dedicated vertical terminations draw through a chimney on their top. Matched by code
    // so it works for vanilla chimneys and any mod's variant.
    if (
      node is BlockPipePassthrough or BlockPipeOutlet
      && face == BlockFacing.UP
      && neighbour.Code?.Path.Contains("chimney") == true
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
    // Chimney draw (gas only) - ChimneyGasDrawRate L/s per chimney-capped top connector. Each
    // drawing chimney puffs smoke so the venting is visible.
    float vented = 0f;
    if (!liquid && vents.Count > 0 && state.Volume > 0)
    {
      vented = Math.Min(
        state.Volume,
        vents.Count * PpexValues.ChimneyGasDrawRate
      );
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
