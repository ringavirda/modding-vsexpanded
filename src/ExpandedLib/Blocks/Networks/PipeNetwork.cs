using System;
using System.Collections.Generic;
using ExpandedLib.Fluids;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Concrete <see cref="BlockNetwork"/> for the pipe system. Owns a single-medium
/// <see cref="PipeNetworkState"/> and implements production/consumption, pressure, merge/split
/// and tick logic. Gas uses <see cref="TryProduceGas"/>/<see cref="TryConsumeGas"/>, water uses
/// <see cref="TryProduceLiquid"/>/<see cref="TryConsumeLiquid"/>; each pair refuses a run
/// already carrying the other medium.
/// </summary>
public class PipeNetwork : BlockNetwork
{
  public override string NetworkType => "pipe";

  // Optional gas-vent strategy (chimney draw), supplied by the content mod at RegisterNetworkType.
  // Null in a bare-constructed network (e.g. tests) → every open end is a leak, nothing vents.
  private readonly IPipeVentStrategy? _vent;

  // The medium policy (compatibility / priority) - the same injection seam as the vent strategy.
  // Defaults to the shared ExLiquids taxonomy, which always knows the four built-ins, so a
  // bare-constructed network (tests) behaves identically to a registered one.
  private readonly IMediumTaxonomy _taxonomy;

  public PipeNetwork(
    BlockNetworkModSystem system,
    IPipeVentStrategy? vent = null,
    IMediumTaxonomy? taxonomy = null
  )
    : base(system)
  {
    _vent = vent;
    _taxonomy = taxonomy ?? ExLiquids.Taxonomy;
  }

  /// <summary>
  /// Live pipe state, or <c>null</c> when empty. Backed by the base
  /// <see cref="BlockNetwork.State"/> so the typed accessor and base code share one object.
  /// </summary>
  public new PipeNetworkState? State
  {
    get => base.State as PipeNetworkState;
    private set => base.State = value;
  }

  public override void RestoreState(object? state)
  {
    State = state as PipeNetworkState;
  }

  // Per-second throughput accumulators (litres). Producers/consumers add to these;
  // OnTick folds them into State.FlowRate once a second and resets them.
  private float _producedAccum;
  private float _consumedAccum;

  // Raw per-tick throughput is bursty (a boiler draws its whole intake buffer at once, then
  // idles), so we display an EMA and only clear a drained run back to "empty" after a few
  // seconds with no real flow, so the medium label survives brief drains.
  private float _smoothedFlow;
  private float _secondsSinceFlow;
  private const float FlowSmoothingAlpha = 0.3f;
  private const float EmptyClearDelaySeconds = 3f;

  // In-game day stamp for natural water evaporation (see OnTick). -1 until the first
  // tick stamps it, so no evaporation is charged for time the network was unloaded.
  private double _lastEvapDays = -1;

  // Seconds the run has sat at/above its weakest pipe's burst pressure; at
  // PipeOverpressureSeconds a pipe bursts. Transient (a reload resets the grace).
  private float _overpressureSeconds;

  #region State inheritance

  public override void InheritStateFrom(BlockNetwork source)
  {
    if (source is not PipeNetwork other)
      return;
    State = other.State;
  }

  #endregion

  #region Gas pool

  /// <summary>
  /// Injects up to <paramref name="volume"/> L of gas. Gas may overflow above 1 atm up to
  /// <paramref name="maxOutputPressure"/> · MaxVolume (each producer's own choke). Returns
  /// <c>true</c> if any gas was accepted or the type/temperature changed.
  /// </summary>
  public bool TryProduceGas(
    float volume,
    float temperature,
    string gasType,
    IBlockAccessor blockAccessor,
    float maxOutputPressure = 1f,
    bool bypassLeakCap = false
  )
  {
    State ??= new PipeNetworkState();
    // A run already carrying water rejects gas - one medium per network. But a physically empty
    // run (Volume <= 0) only keeps its old medium label as a display ghost during the brief
    // empty-clear delay (see OnTick); a new medium must be free to re-claim those empty pipes
    // rather than being latched out until the label clears.
    if (State.Volume > 0f && !_taxonomy.Compatible(State.MediumType, gasType))
      return false;
    State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;

    // The run can't be charged past the weakest pipe's burst rating, and a leaking run vents
    // anything over 1 atm - so clamp the producer's choke by both. bypassLeakCap lifts the
    // 1-atm clamp so a caller that hand-limits volume to the leak rate can push that trickle
    // straight through (it flows in and leaks back out) without backing up.
    float ceilingPressure = Math.Min(
      maxOutputPressure,
      MinBurstPressure(blockAccessor)
    );
    if (State.IsLeaking && !bypassLeakCap)
      ceilingPressure = Math.Min(ceilingPressure, 1f);

    float ceiling = ceilingPressure * State.MaxVolume;
    float actualVolume = Math.Min(volume, ceiling - State.Volume);

    if (actualVolume > 0 || State.Volume <= 0)
    {
      float totalVol = State.Volume + actualVolume;
      if (totalVol > 0)
      {
        State.Temperature =
          (State.Volume * State.Temperature + actualVolume * temperature)
          / totalVol;
      }

      if (State.Volume <= 0)
        State.MediumType = gasType;
      else if (actualVolume > 0)
        State.MediumType = _taxonomy.HigherPriority(State.MediumType, gasType);

      if (actualVolume > 0)
      {
        State.Volume += actualVolume;
        _producedAccum += actualVolume;
      }
      State.Pressure = PipeNetworkState.ComputeGasPressure(
        State.Volume,
        State.MaxVolume
      );
      BroadcastUpdate(blockAccessor);
      return true;
    }

    // Network is at its choke - only upgrade the gas type if needed.
    if (State.MediumType != gasType)
    {
      string upgraded = _taxonomy.HigherPriority(State.MediumType, gasType);
      if (upgraded != State.MediumType)
      {
        State.MediumType = upgraded;
        BroadcastUpdate(blockAccessor);
      }
    }

    return false;
  }

  /// <summary>
  /// Like <see cref="TryProduceGas"/> but returns the litres actually accepted (0 when nothing
  /// fit). The canonical call for producers that need the accepted volume (boiler steam push,
  /// pressure-valve overflow).
  /// </summary>
  public float ProduceGasMeasured(
    float volume,
    float temperature,
    string gasType,
    IBlockAccessor blockAccessor,
    float maxOutputPressure = 1f,
    bool bypassLeakCap = false
  )
  {
    float before = State?.Volume ?? 0f;
    TryProduceGas(
      volume,
      temperature,
      gasType,
      blockAccessor,
      maxOutputPressure,
      bypassLeakCap
    );
    return Math.Max(0f, (State?.Volume ?? 0f) - before);
  }

  /// <summary>
  /// Withdraws up to <paramref name="requestedVolume"/> litres of gas from this network.
  /// Returns the actual amount consumed (0 on a water run). Broadcasts if volume changed.
  /// </summary>
  public float TryConsumeGas(
    float requestedVolume,
    IBlockAccessor blockAccessor
  )
  {
    if (State == null || State.IsLiquid)
      return 0f;

    float available = Math.Min(requestedVolume, State.Volume);
    if (available > 0)
    {
      State.Volume -= available;
      _consumedAccum += available;
      State.Pressure = PipeNetworkState.ComputeGasPressure(
        State.Volume,
        State.MaxVolume
      );
      BroadcastUpdate(blockAccessor);
    }
    return available;
  }

  #endregion

  #region Liquid pool

  /// <summary>
  /// Injects up to <paramref name="volume"/> litres of water into the network and sets
  /// the liquid pressure (the pump drives both). Water temperature blends volume-weighted.
  /// Returns <c>true</c> if any water was accepted. Refuses a run already carrying gas.
  /// </summary>
  public bool TryProduceLiquid(
    float volume,
    float temperature,
    float setPressure,
    IBlockAccessor blockAccessor
  )
  {
    State ??= new PipeNetworkState();
    // A run already carrying gas rejects water - one medium per network. A physically empty run
    // (Volume <= 0) only keeps its old gas label as a display ghost during the empty-clear delay,
    // so let water re-claim those empty pipes rather than latching the stale label (mirror of the
    // guard in TryProduceGas).
    if (State.Volume > 0f && !_taxonomy.Compatible(State.MediumType, "Water"))
      return false;
    State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
    // Record the pump's commanded pressure; it's realised as the run's pressure only once the
    // line is brim-full (below that the pressure tracks the fill ratio).
    State.FeedPressure = setPressure;

    float actual = Math.Min(volume, State.MaxVolume - State.Volume);
    if (actual <= 0f)
    {
      // Already brim-full - no more water fits, but keep the pressure in step with the
      // (possibly changed) feed pressure.
      State.Pressure = PipeNetworkState.ComputeLiquidPressure(
        State.Volume,
        State.MaxVolume,
        setPressure
      );
      return false;
    }

    float total = State.Volume + actual;
    if (total > 0)
      State.Temperature =
        (State.Volume * State.Temperature + actual * temperature) / total;

    State.MediumType = "Water";
    State.Volume = total;
    State.Pressure = PipeNetworkState.ComputeLiquidPressure(
      total,
      State.MaxVolume,
      setPressure
    );
    _producedAccum += actual;
    BroadcastUpdate(blockAccessor);
    return true;
  }

  /// <summary>
  /// Like <see cref="TryProduceLiquid"/> but returns the litres actually accepted. The canonical
  /// call for producers that need the accepted volume (fluid intake).
  /// </summary>
  public float ProduceLiquidMeasured(
    float volume,
    float temperature,
    float setPressure,
    IBlockAccessor blockAccessor
  )
  {
    float before = State?.Volume ?? 0f;
    TryProduceLiquid(volume, temperature, setPressure, blockAccessor);
    return Math.Max(0f, (State?.Volume ?? 0f) - before);
  }

  /// <summary>
  /// Withdraws up to <paramref name="requestedVolume"/> litres of water from the network.
  /// Returns the actual amount consumed (0 on a gas run), carrying <see cref="PipeNetworkState.Temperature"/>.
  /// </summary>
  public float TryConsumeLiquid(
    float requestedVolume,
    IBlockAccessor blockAccessor
  )
  {
    if (State == null || !State.IsLiquid)
      return 0f;

    float available = Math.Min(requestedVolume, State.Volume);
    if (available > 0)
    {
      State.Volume -= available;
      _consumedAccum += available;
      // Draining drops the line below brim-full, so its pressure falls back to the fill ratio.
      State.Pressure =
        State.Volume <= 0f
          ? 0f
          : PipeNetworkState.ComputeLiquidPressure(
            State.Volume,
            State.MaxVolume,
            State.FeedPressure
          );
      BroadcastUpdate(blockAccessor);
    }
    return available;
  }

  #endregion

  #region Merge / Split

  /// <summary>
  /// The most a pool may hold when its nodes are merged or split. A liquid can't be packed past
  /// <see cref="PipeNetworkState.MaxVolume"/> (1 atm). A gas is compressible: it may sit above
  /// 1 atm up to the weakest pipe's burst rating, so a re-walk (e.g. a valve toggling) must keep
  /// that over-pressure instead of dumping the run back down to 1 atm.
  /// </summary>
  private float PoolVolumeCeiling(
    bool liquid,
    float maxVolume,
    IBlockAccessor world
  ) => liquid ? maxVolume : ComputeMinBurstPressure(world) * maxVolume;

  public override void OnMerge(BlockNetwork other, IBlockAccessor world)
  {
    if (other is not PipeNetwork otherPipe)
      return;

    if (otherPipe.State == null)
    {
      if (State != null)
        State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
      return;
    }

    if (State == null)
    {
      State = otherPipe.State;
      State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
      State.Volume = Math.Min(
        State.Volume,
        PoolVolumeCeiling(State.IsLiquid, State.MaxVolume, world)
      );
      return;
    }

    State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;

    // Incompatible media (gas joined to water) can't blend - the larger run wins, the
    // smaller's content is discarded.
    if (!_taxonomy.Compatible(State.MediumType, otherPipe.State.MediumType))
    {
      if (otherPipe.State.Volume > State.Volume)
        State = otherPipe.State;
      State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
      State.Volume = Math.Min(
        State.Volume,
        PoolVolumeCeiling(State.IsLiquid, State.MaxVolume, world)
      );
      State.Pressure = State.IsLiquid
        ? PipeNetworkState.ComputeLiquidPressure(
          State.Volume,
          State.MaxVolume,
          State.FeedPressure
        )
        : PipeNetworkState.ComputeGasPressure(State.Volume, State.MaxVolume);
      return;
    }

    // Same medium - blend volume-weighted temperature and combine volume.
    float total = State.Volume + otherPipe.State.Volume;
    if (total > 0)
    {
      State.Temperature =
        (
          State.Volume * State.Temperature
          + otherPipe.State.Volume * otherPipe.State.Temperature
        ) / total;
    }

    // Gas runs resolve the dominant type by priority; water keeps its medium.
    if (!State.IsLiquid)
    {
      if (State.Volume <= 0)
        State.MediumType = otherPipe.State.MediumType;
      else if (otherPipe.State.Volume > 0)
        State.MediumType = _taxonomy.HigherPriority(
          State.MediumType,
          otherPipe.State.MediumType
        );
    }

    State.Volume = Math.Min(
      total,
      PoolVolumeCeiling(State.IsLiquid, State.MaxVolume, world)
    );
    if (State.IsLiquid)
    {
      // Keep the stronger pump's feed pressure, then derive the run's pressure from the
      // combined fill.
      State.FeedPressure = Math.Max(
        State.FeedPressure,
        otherPipe.State.FeedPressure
      );
      State.Pressure = PipeNetworkState.ComputeLiquidPressure(
        State.Volume,
        State.MaxVolume,
        State.FeedPressure
      );
    }
    else
      State.Pressure = PipeNetworkState.ComputeGasPressure(
        State.Volume,
        State.MaxVolume
      );
  }

  public override void OnSplitFragment(
    BlockNetwork original,
    IBlockAccessor world
  )
  {
    if (original is not PipeNetwork origPipe || origPipe.State == null)
    {
      State = null;
      return;
    }

    int origCount = Math.Max(1, original.Nodes.Count);
    float maxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
    bool liquid = origPipe.State.IsLiquid;
    // Each fragment keeps its proportional share of the volume, which preserves the run's
    // pressure (a gas fragment may carry over-pressure, so cap at the burst ceiling, not 1 atm).
    float frag = Math.Min(
      origPipe.State.Volume / origCount * Nodes.Count,
      PoolVolumeCeiling(liquid, maxVolume, world)
    );

    if (frag <= 0f)
    {
      State = null;
      return;
    }
    State = new PipeNetworkState
    {
      MaxVolume = maxVolume,
      Volume = frag,
      Temperature = origPipe.State.Temperature,
      MediumType = origPipe.State.MediumType,
      FeedPressure = origPipe.State.FeedPressure,
      Pressure = liquid
        ? PipeNetworkState.ComputeLiquidPressure(
          frag,
          maxVolume,
          origPipe.State.FeedPressure
        )
        : PipeNetworkState.ComputeGasPressure(frag, maxVolume),
    };
  }

  #endregion

  #region Tick

  public override void OnTick(
    IBlockAccessor blockAccessor,
    float dt,
    BlockNetworkModSystem manager
  )
  {
    float instantFlow = Math.Max(_producedAccum, _consumedAccum);
    _producedAccum = 0f;
    _consumedAccum = 0f;

    // Refresh the weakest-pipe rating once per tick (picks up chunk load/unload); producer
    // calls between ticks reuse it at O(1).
    _minBurstCache = null;

    if (State == null)
      return;

    // Smooth the displayed throughput and track how long the run has been genuinely idle.
    _smoothedFlow += (instantFlow - _smoothedFlow) * FlowSmoothingAlpha;
    if (_smoothedFlow < 0.01f)
      _smoothedFlow = 0f;
    if (instantFlow > 0.01f)
      _secondsSinceFlow = 0f;
    else
      _secondsSinceFlow += dt;

    bool changed = false;
    bool liquid = State.IsLiquid;

    State.MaxVolume = Nodes.Count * ExlibValues.LitresPerPipe;
    // Gas pressure is the volume ratio; a liquid's is the fill ratio until brim-full, then the
    // pump-set feed pressure.
    float newPressure = liquid
      ? PipeNetworkState.ComputeLiquidPressure(
        State.Volume,
        State.MaxVolume,
        State.FeedPressure
      )
      : PipeNetworkState.ComputeGasPressure(State.Volume, State.MaxVolume);
    if (Math.Abs(State.Pressure - newPressure) > 0.02f)
    {
      State.Pressure = newPressure;
      changed = true;
    }

    if (Math.Abs(State.FlowRate - _smoothedFlow) > 0.01f)
    {
      State.FlowRate = _smoothedFlow;
      changed = true;
    }

    // Particle density for any open-end leaks this tick, scaled by the network-total leak
    // rate (NOT the opening count): gas wisps ramp over 1→8 L/s, water spray over 1→5 L/s.
    float gasLeakRate = Math.Min(
      Math.Max(0f, State.Volume - State.MaxVolume),
      ExlibValues.GasLeakRate
    );
    float gasLeakFrac = Math.Clamp(
      (gasLeakRate - 1f) / (ExlibValues.GasLeakRate - 1f),
      0f,
      4f
    );
    float waterLeakFrac = Math.Clamp((State.Volume - 1f) / 4f, 0f, 1f);

    // Single pass: detect and classify open connectors - a chimney on the TOP connector of a
    // passthrough/outlet is a gas vent (not a leak), an air-exposed end is a leak; count
    // consumers too.
    int consumers = 0;
    int totalLeaks = 0;
    var chimneyVents = new List<BlockPos>();
    foreach (var pos in Nodes)
    {
      var be = blockAccessor.GetBlockEntity(pos);
      if (be is IPipeNode)
        consumers++;

      if (blockAccessor.GetBlock(pos) is not BlockNetworkNode node)
        continue;

      BlockFacing[] openFaces = manager.GetOpenConnectorFaces(
        blockAccessor,
        pos,
        node
      );
      if (openFaces.Length == 0)
        continue;

      int airOpen = 0;
      for (int i = 0; i < openFaces.Length; i++)
      {
        BlockFacing face = openFaces[i];
        BlockPos nPos = pos.AddCopy(face);
        Block neighbour = blockAccessor.GetBlock(nPos);
        // A vent (e.g. a chimney on the open TOP connector) draws gas away rather than leaking it -
        // the content mod's strategy decides. A vent face is not counted as a leak.
        if (
          _vent != null
          && _vent.TryClassifyVent(
            blockAccessor,
            node,
            pos,
            face,
            neighbour,
            out BlockPos ventPos
          )
        )
        {
          chimneyVents.Add(ventPos);
          continue;
        }
        if (neighbour.FirstCodePart() == "air")
          openFaces[airOpen++] = face;
      }
      if (airOpen == 0)
        continue;

      totalLeaks += airOpen;

      BlockFacing[] leakFaces =
        airOpen == openFaces.Length ? openFaces : openFaces[..airOpen];
      if (be is INetworkNode nodeEntity && State.Volume > 0)
      {
        // A pipe overrides OnLeak to spray leak particles (water sprays out like a poured bucket, gas
        // wisps out); every other node takes the default no-op. Nodes also get the open-connectors
        // hook they may react to.
        nodeEntity.OnLeak(
          leakFaces,
          liquid,
          liquid ? waterLeakFrac : gasLeakFrac
        );
        nodeEntity.OnOpenConnectorsChanged(leakFaces);
      }
    }

    if (State.OpeningsCount != totalLeaks)
    {
      State.OpeningsCount = totalLeaks;
      changed = true;
    }

    // Vent draw (gas only) - the content mod's strategy pulls gas out through any vents (chimneys)
    // and plays the venting feedback. A network with no strategy vents nothing.
    float vented = _vent?.Vent(chimneyVents, State, liquid, manager) ?? 0f;
    if (vented > 0f)
    {
      _consumedAccum += vented;
      changed = true;
    }

    // Leak loss - a gas leak is pressure relief (a small FIXED rate regardless of open-end
    // count, so bulk venting needs a chimney/stack); a water leak drains at a fixed rate.
    if (totalLeaks > 0 && State.Volume > 0f)
    {
      if (liquid)
      {
        float lost = Math.Min(State.Volume, ExlibValues.LiquidLeakRate * dt);
        State.Volume -= lost;
        if (State.Volume <= 0f)
          State.Pressure = 0f;
      }
      else
      {
        float lost = Math.Min(State.Volume, ExlibValues.GasLeakRate);
        State.Volume -= lost;
        if (State.Temperature > 20f)
          State.Temperature = Math.Max(20f, State.Temperature - 5.0f);
      }
      changed = true;
    }

    // Natural evaporation of a water run, measured off the calendar so it's independent of
    // tick cadence and charges nothing for time spent unloaded (same rate as the boiler).
    double nowDays = manager.ServerWorld?.Calendar?.TotalDays ?? -1;
    if (nowDays >= 0)
    {
      if (liquid && _lastEvapDays >= 0 && State.Volume > 0f)
      {
        float evap = (float)(
          ExlibValues.EvaporationLitresPerDay * (nowDays - _lastEvapDays)
        );
        if (evap > 0f)
        {
          State.Volume = Math.Max(0f, State.Volume - evap);
          if (State.Volume <= 0f)
            State.Pressure = 0f;
          changed = true;
        }
      }
      _lastEvapDays = nowDays;
    }

    // Keep the broadcast gas pressure in step after venting / leaking.
    if (!liquid && (chimneyVents.Count > 0 || totalLeaks > 0))
      State.Pressure = PipeNetworkState.ComputeGasPressure(
        State.Volume,
        State.MaxVolume
      );

    // Passive cooling of an idle gas run (no consumers drawing it).
    if (
      !liquid
      && State.Volume > 0
      && State.Temperature > 20f
      && consumers == 0
    )
    {
      State.Temperature = Math.Max(20f, State.Temperature - 2.0f);
      changed = true;
    }

    // Clear empty state only once drained AND idle for a few seconds, so a push-and-drain
    // water line (near 0 L while busy) keeps its "Water" label instead of flickering.
    if (State.Volume <= 0 && _secondsSinceFlow >= EmptyClearDelaySeconds)
    {
      State = null;
      _smoothedFlow = 0f;
      changed = true;
    }

    if (changed)
      BroadcastUpdate(blockAccessor);

    // Over-pressure timer: a sealed, over-fed run sits exactly at its burst pressure with
    // nowhere to go. Hold there for PipeOverpressureSeconds and a pipe lets go; any relief
    // dropping the pressure below the rating resets the grace.
    bool pressureFailure = false;
    if (State != null)
    {
      float minBurst = MinBurstPressure(blockAccessor);
      bool overPressure =
        !State.IsLiquid
        && State.Volume > 0f
        && minBurst < float.MaxValue
        && State.Pressure >= minBurst - 0.001f;

      if (overPressure)
      {
        _overpressureSeconds += dt;
        if (_overpressureSeconds >= ExlibValues.PipeOverpressureSeconds)
        {
          pressureFailure = true;
          _overpressureSeconds = 0f;
        }
      }
      else if (_overpressureSeconds > 0f)
        _overpressureSeconds = 0f;
    }

    // Executed last so we never mutate the node set while reading it. Each burst removes a
    // node (fracturing the run) and drops the pipe's materials.
    if (State != null && pressureFailure)
    {
      foreach (var pos in CollectBursts(blockAccessor))
        ExecuteBurst(pos, blockAccessor, manager);
    }
  }

  // Consulted by every TryProduceGas call, so cache it; only changes with the node set
  // (invalidated via OnTopologyChanged) plus a once-per-tick refresh in OnTick.
  private float? _minBurstCache;

  /// <summary>Drops topology-derived caches when the manager changes the node set.</summary>
  public override void OnTopologyChanged()
  {
    _minBurstCache = null;
  }

  /// <summary>The weakest pipe's burst pressure (atm) across the whole run - the rating
  /// that caps how far the gas pool can be pressurised. <see cref="float.MaxValue"/> when
  /// the run holds no pipes (e.g. only machine ports).</summary>
  private float MinBurstPressure(IBlockAccessor world) =>
    _minBurstCache ??= ComputeMinBurstPressure(world);

  private float ComputeMinBurstPressure(IBlockAccessor world)
  {
    float minBurst = float.MaxValue;
    foreach (var pos in Nodes)
      if (world.GetBlock(pos) is IBurstablePipe p && p.CanBurst)
        minBurst = Math.Min(minBurst, p.BurstPressure);
    return minBurst;
  }

  // Per-instance fallback RNG; the burst path prefers the world RNG (seedable in tests) so burst
  // selection is deterministic under a seeded world and not shared across every network in the process.
  private readonly Random _rand = new();

  /// <summary>
  /// Finds the pipe that should fail this tick: one random pipe that has held its burst
  /// pressure past the over-pressure grace. Called only when a pressure failure is due.
  /// </summary>
  private List<BlockPos> CollectBursts(IBlockAccessor world)
  {
    var result = new List<BlockPos>();
    if (State == null)
      return result;

    var pressureCandidates = new List<BlockPos>();

    foreach (var pos in Nodes)
    {
      if (world.GetBlock(pos) is not IBurstablePipe pipe || !pipe.CanBurst)
        continue;

      if (State.Pressure >= pipe.BurstPressure - 0.001f)
        pressureCandidates.Add(pos);
    }

    if (pressureCandidates.Count > 0)
    {
      Random rand = NetworkSystem?.ServerWorld?.Rand ?? _rand;
      result.Add(pressureCandidates[rand.Next(pressureCandidates.Count)]);
    }

    return result;
  }

  /// <summary>
  /// Breaks a failed pipe: drops its materials, removes it from the graph, and sets the
  /// cell to air. The graph removal handles the network fracture.
  /// </summary>
  private static void ExecuteBurst(
    BlockPos pos,
    IBlockAccessor world,
    BlockNetworkModSystem manager
  )
  {
    Block block = world.GetBlock(pos);
    if (block.BlockId == 0)
      return;

    var sworld = manager.ServerWorld;
    if (sworld != null)
    {
      ItemStack[]? drops = block.GetDrops(sworld, pos, null);
      if (drops != null)
      {
        foreach (var ds in drops)
          sworld.SpawnItemEntity(ds, pos.ToVec3d().Add(0.5, 0.5, 0.5));
      }

      // A bursting pipe vents a puff of vapour and pops with a muffled small explosion
      // (server-spawned, so it broadcasts to nearby clients).
      ExParticles.SteamPlume(sworld, pos, 18);
      ExSounds.PlayLocal(sworld, pos, ExSounds.SmallExplosion, 0.4f, 24f);
    }

    manager.RemoveNode(world, pos);
    world.SetBlock(0, pos);
  }

  #endregion
}
