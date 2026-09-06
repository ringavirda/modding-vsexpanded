using System;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Catalogues;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkPipe.BlockEntities;

/// <summary>
/// The steam condenser's logic. Each tick it passes water through its W/E faces - the fuller side is
/// the inlet, the other the outlet, and the inlet pressure is preserved downstream - and condenses
/// steam drawn from the north line into that through-flow. With no outlet piped, the backed-up water
/// and condensate spray out of the open face; with no water line at all, drawn steam vents as gas.
/// </summary>
[BlockEntityRegister]
public class BlockEntitySteamCondenser : ExBlockEntity {
  private long _tickId;

  // Client-display mirror, synced via the tree.
  [Persist("condensing")]
  private bool _condensing;

  private BlockSteamCondenser? CondenserBlock => Block as BlockSteamCondenser;

  /// <summary>The pipe network across one of the condenser's connector faces, or <c>null</c> when
  /// the adjacent pipe has no connector facing back.</summary>
  private PipeNetwork? ConnectedNetwork(BlockFacing connectorFace) =>
    this.ConnectedNetwork<PipeNetwork>(connectorFace);

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    if (api.Side == EnumAppSide.Server) {
      _tickId = RegisterGameTickListener(OnTick, 1000);
    }
  }

  private void OnTick(float dt) {
    if (CondenserBlock == null)
      return;

    var ba = Api.World.BlockAccessor;

    PipeNetwork? steamNet = ConnectedNetwork(CondenserBlock.SteamInletFace);
    BlockFacing faceA = CondenserBlock.SideAFace;
    BlockFacing faceB = CondenserBlock.SideBFace;
    PipeNetwork? netA = ConnectedNetwork(faceA);
    PipeNetwork? netB = ConnectedNetwork(faceB);

    bool condensing = Process(steamNet, netA, faceA, netB, faceB, dt, ba);

    if (condensing != _condensing) {
      _condensing = condensing;
      MarkDirty(true);
    }
  }

  /// <summary>
  /// Runs the water line through the W↔E faces (fuller side is the inlet) and condenses steam from
  /// the north line into that through-flow, preserving the inlet's pressure downstream. Returns
  /// <c>true</c> if any steam was condensed this tick, which drives the HUD state.
  /// </summary>
  private bool Process(
    PipeNetwork? steamNet,
    PipeNetwork? netA,
    BlockFacing faceA,
    PipeNetwork? netB,
    BlockFacing faceB,
    float dt,
    IBlockAccessor ba
  ) {
    // Only water-capable sides count for the water line; a gas run is unplumbed for water.
    PipeNetwork? wa = CanTakeWater(netA) ? netA : null;
    PipeNetwork? wb = CanTakeWater(netB) ? netB : null;

    bool steam = HasCondensableGas(steamNet, out float rawFactor);
    // Litres of condensate = gas drawn / expansion factor. The medium def may override the factor;
    // otherwise the iiex steam-expansion default applies, since exlib carries no iiex constant.
    float condenseFactor =
      rawFactor > 0f ? rawFactor : IiexValues.SteamExpansionFactor;
    float steamTemp = steam
      ? steamNet!.State!.Temperature
      : IiexValues.BoilingPoint;

    // Both water faces on the same run (a loop): nothing to pass, just drop in condensate.
    if (wa != null && ReferenceEquals(wa, wb)) {
      if (!steam)
        return false;
      float looped =
        steamNet!.TryConsumeGas(IiexValues.CondenserSteamPerSecond * dt, ba)
        / condenseFactor;
      if (looped <= 0f)
        return false;
      InjectWater(wa, looped, steamTemp, wa.State?.Pressure ?? 0f, ba);
      return true;
    }

    // Inlet = the fuller water side; outlet = the other (each paired with its face).
    PipeNetwork? inNet,
      outNet;
    BlockFacing outFace;
    if (LiquidVolumeOf(wa) >= LiquidVolumeOf(wb))
      (inNet, outNet, outFace) = (wa, wb, faceB);
    else
      (inNet, outNet, outFace) = (wb, wa, faceA);

    // No water line on either side: there is nowhere to condense into, so drawn steam vents out of
    // the condenser as gas, capped at the pipe gas-leak rate.
    if (inNet == null && outNet == null) {
      if (!steam)
        return false;
      float ventGas = steamNet!.TryConsumeGas(
        Math.Min(
          IiexValues.CondenserSteamPerSecond * dt,
          ExlibValues.GasLeakRate
        ),
        ba
      );
      if (ventGas > 0f) {
        ExParticles.GasVent(
          Api.World,
          Pos,
          outFace,
          steamNet.State!.MediumType
        );
        ExSounds.PlayAt(
          Api.World,
          Pos,
          ExSounds.Swoosh,
          range: 24f,
          volume: 0.6f
        );
      }
      return false;
    }

    // Steam condenses into its (much smaller) hot-water volume, merged into the line below.
    float condensed = 0f;
    if (steam) {
      float used = steamNet!.TryConsumeGas(
        IiexValues.CondenserSteamPerSecond * dt,
        ba
      );
      condensed = used / condenseFactor;
    }

    // No outlet piped: the water line backs up and leaks out of the open outlet face. Drain what the
    // open end can shed from the inlet, add the condensate and spray it out; that water is lost.
    // Capped at the pipe water-leak rate, like any open-ended run.
    if (outNet == null) {
      float drained =
        inNet != null
          ? inNet.TryConsumeLiquid(ExlibValues.LiquidLeakRate * dt, ba)
          : 0f;
      if (drained + condensed <= 0f)
        return false;
      ExParticles.WaterJet(Api.World, Pos, outFace);
      ExSounds.SplashSound(Api.World, Pos);
      return condensed > 0f;
    }

    // Reserve outlet space for the condensate first, then move as much through-flow as fits.
    float outFree = Math.Max(
      0f,
      outNet.Nodes.Count * ExlibValues.LitresPerPipe - LiquidVolumeOf(outNet)
    );
    float condIn = Math.Min(condensed, outFree);
    float passSpace = outFree - condIn;

    float inTemp = inNet?.State?.Temperature ?? ExlibValues.AmbientTemperature;
    float inPress = inNet?.State?.Pressure ?? 0f;
    float move =
      inNet != null && passSpace > 0f
        ? inNet.TryConsumeLiquid(
          Math.Min(IiexValues.CondenserWaterThroughput * dt, passSpace),
          ba
        )
        : 0f;

    float total = condIn + move;
    if (total <= 0f)
      return false;

    float mixedTemp = (move * inTemp + condIn * steamTemp) / total;
    mixedTemp = Math.Clamp(
      mixedTemp,
      ExlibValues.AmbientTemperature,
      IiexValues.BoilingPoint - 1f
    );
    outNet.TryProduceLiquid(total, mixedTemp, inPress, ba);
    return condIn > 0f;
  }

  /// <summary>Adds water to a network as hot (sub-boiling) liquid at the given pressure.</summary>
  private static void InjectWater(
    PipeNetwork net,
    float amount,
    float temp,
    float pressure,
    IBlockAccessor ba
  ) {
    net.TryProduceLiquid(
      amount,
      Math.Clamp(
        temp,
        ExlibValues.AmbientTemperature,
        IiexValues.BoilingPoint - 1f
      ),
      pressure,
      ba
    );
  }

  /// <summary>Litres of water in <paramref name="net"/>, or 0 if it carries gas / is empty.</summary>
  private static float LiquidVolumeOf(PipeNetwork? net) =>
    net?.State is { IsLiquid: true } s ? s.Volume : 0f;

  /// <summary>Whether <paramref name="net"/> can receive water: a water run, or one that has not
  /// claimed a medium yet. A gas run would reject it.</summary>
  private static bool CanTakeWater(PipeNetwork? net) =>
    net != null
    && (
      net.State == null
      || net.State.IsLiquid
      || net.State.MediumType.Length == 0
    );

  /// <summary>Whether <paramref name="net"/> carries a gas that the liquid taxonomy declares
  /// condensable, which keeps the condenser medium-agnostic. The medium's condensation volume factor
  /// is returned in <paramref name="volumeFactor"/>; 0 means the caller's own default applies.</summary>
  private static bool HasCondensableGas(
    PipeNetwork? net,
    out float volumeFactor
  ) {
    volumeFactor = 0f;
    if (net?.State is not { Volume: > 0f } s || s.IsLiquid)
      return false;
    return ExLiquids.Taxonomy.CondensationTarget(
      s.MediumType,
      out _,
      out volumeFactor
    );
  }

  public override void OnBlockRemoved() {
    if (_tickId != 0)
      UnregisterGameTickListener(_tickId);
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    if (_tickId != 0)
      UnregisterGameTickListener(_tickId);
    base.OnBlockUnloaded();
  }

  protected override void DeclareState(ExBlockState state) { }

  public override void GetBlockInfo(
    IPlayer forPlayer,
    System.Text.StringBuilder dsc
  ) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        _condensing
          ? "iiex:condenser-info-condensing"
          : "iiex:condenser-info-idle"
      )
    );
  }
}
