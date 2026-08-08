using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;

/// <summary>
/// Block entity for the cowper-stove multiblock, a regenerative heat exchanger. It absorbs heat from
/// the furnace exhaust into its brick core (faster with a burning coal pile below), then reheats air
/// passing through into the hot blast that boosts the blast furnace.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCowperStove : BlockEntityMultiblockStructure {
  private BlockFacing _connectorFace = BlockFacing.SOUTH;
  private float _internalTemperature = ExlibValues.AmbientTemperature;
  private string _lastStatus = Lang.Get("smex:cowperstove-status-idle");
  private long _lastHeatSoundMs;

  // Config tunables (see SmexValues), cached in fields by CacheTunables so the tick body does not
  // read the static config per use.
  private float _factorAnthracite;
  private float _factorOtherCoal;
  private float _factorDefault;
  private float _coolingSpeedExhaust;
  private float _coolingSpeedAir;
  private float _exhaustAttenuation;
  private float _maxTemperature;
  private float _intakeVolume;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);

    CacheTunables();

    // The exhaust connector sits on the stove's local-south face, rotated with the block.
    _connectorFace = ExOrientation.RotateFacing(
      BlockFacing.SOUTH,
      ExOrientation.AngleFromSide(Block.Variant["side"])
    );
  }

  // Pulls the gameplay tunables off the live config. Re-run every production tick, not only at load,
  // so an `/exmod config smex ...` change applies immediately rather than after a chunk reload.
  private void CacheTunables() {
    _factorAnthracite = SmexValues.CowperHeatingSpeedAnthracite;
    _factorOtherCoal = SmexValues.CowperHeatingSpeedOtherCoal;
    _factorDefault = SmexValues.CowperHeatingSpeedDefault;
    _coolingSpeedExhaust = SmexValues.CowperCoolingSpeedExhaust;
    _coolingSpeedAir = SmexValues.CowperCoolingSpeedAir;
    _exhaustAttenuation = SmexValues.CowperExhaustAttenuation;
    _maxTemperature = SmexValues.CowperMaxTemperature;
    _intakeVolume = SmexValues.CowperIntakeVolume;
  }

  #region Abstract implementations

  protected override void UpdateStructureRotation() {
    if (Block == null)
      return;

    // The cowper's structure layout faces opposite its "side" variant, the same +180 convention as
    // the boiler body.
    SetStructureAngle(
      (ExOrientation.AngleFromSide(Block.Variant["side"]) + 180) % 360
    );
  }

  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.Get("smex:structure-incomplete-count", missingCount);

  protected override string GetCompleteMessage() =>
    Lang.Get("smex:cowperstove-complete");

  #endregion

  #region Production tick

  // True for the vanilla coal-pile block family, matched by code path: vanilla blocks carry no
  // attribute to key on without a JSON patch, and the prefix covers every pile variant. The stove
  // then inspects the pile inventory to tell anthracite from other coal.
  private static bool IsCoalPile(Block? block) =>
    block?.Code?.Path.StartsWith("coalpile") == true;

  protected override void OnProductionTick(float dt) {
    if (!StructureComplete)
      return;

    // Pick up any live `/exmod config` tunable change this tick.
    CacheTunables();

    // The stove is a fixed machine port: it draws only from a run whose pipe presents a connector
    // back at the stove's exhaust face. A pipe routed through the adjacent cell with its connectors
    // pointing elsewhere is not plumbed in - the same reciprocity rule as the converter intake and
    // the engines.
    var consumedExhaustVol = 0f;
    float inputExhaustTemp = ExlibValues.AmbientTemperature;
    bool isReceivingExhaust = false;
    if (ConnectedNetwork<PipeNetwork>(_connectorFace) is { } exhaustNet) {
      inputExhaustTemp =
        exhaustNet.State?.Temperature ?? ExlibValues.AmbientTemperature;
      consumedExhaustVol = exhaustNet.TryConsumeGas(
        _intakeVolume,
        Api.World.BlockAccessor
      );
      isReceivingExhaust = consumedExhaustVol > 0;
    }

    bool isAnthracite = false;
    bool hasOtherCoal = false;

    Block blockBelow = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
    if (IsCoalPile(blockBelow)) {
      if (
        Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy())
          is BlockEntityItemPile pile
        && pile.inventory != null
        && !pile.inventory[0].Empty
      ) {
        string? path = pile.inventory[0]?.Itemstack?.Collectible?.Code?.Path;
        if (path != null) {
          if (path.Contains("anthracite"))
            isAnthracite = true;
          else
            hasOtherCoal = true;
        }
      }
    }

    float airVol = 0f;
    float airTemp = ExlibValues.AmbientTemperature;
    string inGasType = "Air";

    BlockPos passthroughPos = GetGlobalPos(0, 1, 2);
    var passthrough =
      Api.World.BlockAccessor.GetBlockEntity(passthroughPos)
      as BlockEntityPipePassthrough;

    // Raw gas present in the passthrough's run. airVol below rounds this up to a full intake for
    // discharge throughput, so the raw figure is kept for the "is air flowing" test.
    float passthroughVol = passthrough?.Volume ?? 0f;
    if (passthroughVol > 0) {
      airTemp = passthrough!.Temperature;
      inGasType = passthrough.Medium;
      airVol = passthroughVol <= _intakeVolume ? _intakeVolume : passthroughVol;
    }

    string newStatus = Lang.Get("smex:cowperstove-status-idle");

    if (isReceivingExhaust && passthroughVol > ExlibValues.LitresPerPipe) {
      // Air and exhaust both present. Closing the air valve cuts supply but leaves the gas already
      // in the passthrough stranded there, and a pressurised run holds well over one pipe's worth,
      // which would latch the stove in "mixing" and refuse to charge. Venting the stranded gas
      // clears it within a tick once the valve is shut; a still-open valve keeps refilling it and
      // stays flagged.
      newStatus = Lang.Get("smex:cowperstove-status-exhaustmix");
      passthrough?.TryConsume(passthroughVol);
    } else if (isReceivingExhaust) {
      newStatus = Lang.Get("smex:cowperstove-status-heatingup");
      float tempDiff = inputExhaustTemp - _internalTemperature;
      if (tempDiff > 0) {
        float factor = isAnthracite
          ? _factorAnthracite
          : (hasOtherCoal ? _factorOtherCoal : _factorDefault);
        // Heat-transfer rates are per-second; scale by dt for tick-independence.
        _internalTemperature += tempDiff * factor * dt;
        _internalTemperature = System.Math.Min(
          _internalTemperature,
          _maxTemperature
        );
        inputExhaustTemp -= tempDiff * _coolingSpeedExhaust * dt;
      }

      SpawnHeatingParticles();
      // Low roar of the regenerator soaking up furnace exhaust.
      ExSounds.PlayThrottled(
        Api,
        Pos,
        ExSounds.Fire,
        ref _lastHeatSoundMs,
        5000,
        0.4f
      );

      BlockPos exhaustOutletPos2 = GetGlobalPos(0, 0, 2);
      if (
        Api.World.BlockAccessor.GetBlockEntity(exhaustOutletPos2)
        is IPipeNode outlet2
      )
        outlet2.TryProduce(
          consumedExhaustVol,
          System.Math.Max(
            ExlibValues.AmbientTemperature,
            inputExhaustTemp * _exhaustAttenuation
          ),
          "Exhaust"
        );
    } else if (airVol > 0) {
      newStatus = Lang.Get("smex:cowperstove-status-heating", inGasType);
      float tempDiff = _internalTemperature - airTemp;
      if (tempDiff > 0) {
        airTemp = _internalTemperature;
        _internalTemperature -= tempDiff * _coolingSpeedAir * dt;
      }

      BlockPos hotAirOutletPos = GetGlobalPos(0, 1, 0);
      if (
        Api.World.BlockAccessor.GetBlockEntity(hotAirOutletPos)
        is IPipeNode hotOutlet
      ) {
        float inputPressure = passthrough?.Pressure ?? 1f;
        var accepted = hotOutlet.TryProduce(
          airVol,
          airTemp,
          inGasType,
          maxOutputPressure: inputPressure > 1f ? inputPressure : 1f
        );
        if (accepted && passthrough != null)
          passthrough.TryConsume(_intakeVolume);
      }
    }

    if (_lastStatus != newStatus) {
      _lastStatus = newStatus;
      MarkDirty(true);
    }

    UpdateHeatsinks();
  }

  private void SpawnHeatingParticles() {
    // The heat column spawns over the central interior column (structure-local (0, *, 1), the
    // heatsink stack), rotated the way GetGlobalPos resolves it: InitForUse(_currentAngle) applied
    // to (x:0, z:1).
    Vec3i d = ExOrientation.RotateOffset(0, 0, 1, _currentAngle);
    int dx = d.X,
      dz = d.Z;

    Vec3d minPos = new(Pos.X + dx + 0.1, Pos.Y + 0.1, Pos.Z + dz + 0.1);
    Vec3d maxPos = new(Pos.X + dx + 0.9, Pos.Y + 3.9, Pos.Z + dz + 0.9);

    ExParticles.RisingPlume(
      Api.World,
      ExParticles.GlowSpark,
      minPos,
      maxPos,
      new Vec3f(-0.2f, 0.5f, -0.2f),
      new Vec3f(0.2f, 1.5f, 0.2f),
      4,
      8,
      1f,
      -0.05f,
      0.2f,
      0.5f,
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, -150f),
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.2f)
    );
  }

  private void UpdateHeatsinks() {
    for (int y = 0; y <= 3; y++) {
      BlockPos hsPos = GetGlobalPos(0, y, 1);
      if (
        Api.World.BlockAccessor.GetBlockEntity(hsPos) is BlockEntityHeatSink hs
        && System.Math.Abs(hs.Temperature - _internalTemperature) > 1f
      ) {
        hs.Temperature = _internalTemperature;
        hs.MarkDirty(true);
      }
    }
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (!StructureComplete) {
      dsc.AppendLine(Lang.Get("smex:structure-incomplete"));
      return;
    }
    dsc.AppendLine(Lang.Get("smex:cowperstove-info-status", _lastStatus));
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat("internalTemperature", _internalTemperature);
    tree.SetString("lastStatus", _lastStatus);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _internalTemperature = tree.GetFloat("internalTemperature");
    _lastStatus = tree.GetString(
      "lastStatus",
      Lang.Get("smex:cowperstove-status-idle")
    );
  }

  #endregion
}
