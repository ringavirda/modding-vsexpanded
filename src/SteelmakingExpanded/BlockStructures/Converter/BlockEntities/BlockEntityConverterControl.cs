using System;
using System.Collections.Generic;
using ExpandedLib;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Metals;
using ExpandedLib.Process;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Renderers;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SteelmakingExpanded.BlockStructures.Converter.BlockEntities;

/// <summary>
/// Control block entity of the Bessemer converter multiblock: owns the operational state machine,
/// the molten charge and the pig-iron to steel refining process. The converter block is a visual and
/// break shell driven from here; peripherals (input tap, gas intake, output start, transmission)
/// resolve from their structure-local offsets through <see cref="GetGlobalPos"/>. The blow is an
/// autothermal acid Bessemer: the pig's own oxidisable content, burned by the blast, is the only heat
/// source, and carbon falls monotonically, so the stop point selects the product - pig, Bessemer
/// steel at target, or soft ingot iron on over-blow. See docs/design/materials.md.
/// </summary>
[BlockEntityRegister]
public partial class BlockEntityConverterControl : BlockEntityMultiblockMachine {
  #region Structure-local peripheral offsets
  private static readonly (int x, int y, int z) TransmissionLocal = (0, -1, 0);
  private static readonly (int x, int y, int z) ConverterLocal = (0, 0, 2);
  private static readonly (int x, int y, int z) GasIntakeLocal = (0, 0, 4);
  private static readonly (int x, int y, int z) InputTapLocal = (1, 1, 2);
  private static readonly (int x, int y, int z) OutputStartLocal = (1, -2, 2);
  #endregion

  #region Tunables (see SmexValues)
  private static int CapacityUnits => SmexValues.BessemerConverterCapacity;
  private static float BlastPerSecond => SmexValues.BessemerBlastPerSecond;
  private static float PowerSpeedThreshold =>
    SmexValues.BessemerPowerSpeedThreshold;
  private static float PourRate => SmexValues.BessemerPourRate;

  // Carbon model + autothermal heat balance. All live config, re-read on every access.
  private static float PigCarbonStart => SmexValues.BessemerPigCarbonStart;
  private static float SteelCarbonTarget =>
    SmexValues.BessemerSteelCarbonTarget;
  private static float OverblowCarbon => SmexValues.BessemerOverblowCarbon;
  private static float CarbonPerBlastLitre =>
    SmexValues.BessemerCarbonPerBlastLitre;
  private static float AutothermalBase => SmexValues.BessemerAutothermalBase;
  private static float HeatPerCarbonUnit =>
    SmexValues.BessemerHeatPerCarbonUnit;
  private static float AutothermalCeiling =>
    SmexValues.BessemerAutothermalCeiling;
  private static float RadiationLoss => SmexValues.BessemerRadiationLoss;
  private static float RefineTemperature =>
    SmexValues.BessemerRefineTemperature;
  private static float ColdScrapLossCoefficient =>
    SmexValues.BessemerColdScrapLossCoefficient;
  private static int ScrapUnitValue => SmexValues.BessemerScrapUnitValue;
  private static float ScrapSteelYield => SmexValues.BessemerScrapSteelYield;
  private static float SteelYield => SmexValues.BessemerSteelYield;
  private static float SlagYield => SmexValues.BessemerSlagYield;

  // Molten-system cooldown speed scaled by the configured coefficient (0.5 cools twice as slowly),
  // so the insulated vessel holds a finished heat long enough to pour it.
  private static float ContentCooldownSpeed =>
    IwexValues.MoltenCooldownSpeed * SmexValues.BessemerCooldownCoefficient;

  // Below this fraction of capacity a hardened residue can be chiselled out instead of breaking the
  // whole converter to salvage it.
  private static float ChiselMaxFraction =>
    SmexValues.BessemerChiselMaxFraction;

  // Molten item codes for the converter's metals, resolved through the shared registry so another mod
  // can redirect a token: molten pig in, Bessemer steel at the target, soft ingot iron on over-blow,
  // and the same iwex:slag the furnaces make.
  private static string PigCode =>
    MetalRegistry.MoltenItemOf("pigiron").ToString();
  private static string SteelCode =>
    MetalRegistry.MoltenItemOf("bessemersteel").ToString();
  private static string IronCode =>
    MetalRegistry.MoltenItemOf("iron").ToString();
  private static string SlagCode =>
    MetalRegistry.MoltenItemOf("slag").ToString();
  #endregion

  #region Operational + charge state
  /// <summary>Current player-selected tilt state of the converter.</summary>
  public ConverterOpState OpState { get; private set; } =
    ConverterOpState.Normal;

  // The metal pool: molten pig on the way in, retyped in place to Bessemer steel at the carbon
  // target, or to soft ingot iron on over-blow. Units are metal units.
  private MoltenCharge? _charge;

  // Carbon fraction of the bath (pig starts near 0.04). Falls as the blow oxidises it; drives the
  // retype and the HUD carbon percentage.
  private float _carbon;

  // Pig mass basis for the mass-balance yields (total pig charged this heat), and the sub-unit carry
  // that keeps the per-tick integer shed from losing fractional mass.
  private int _pigCharged;
  private float _shedCarry;

  // Cold steel scrap charged alongside the pig, in molten units. Pure heat-sink mass that raises
  // T_loss until it melts into the steel at the carbon target; there is no fixed scrap ceiling.
  private int _scrapUnits;

  // The floating slag pool in units, the same iwex:slag the furnaces make. Accumulates during the
  // blow as impurities oxidise; a shallow tilt spills it off the top before the steel beneath.
  private float _moltenSlag;

  private bool _solidified;

  // Heat balance from the last tick, serialized so the client HUD can print the contributors:
  // GetBlockInfo runs client-side and the client never blows the bath.
  private HeatBalance _lastHeatBalance;

  private string _status = Lang.Get("smex:bessemer-status-idle");

  private ToggleAnimator? _toggle;

  // Sound throttles (world-elapsed ms) for the looping ambience. Filling and pouring are
  // mutually exclusive, so they share _lastMoltenSoundMs.
  private long _lastProcessSoundMs;
  private long _lastFireSoundMs;
  private long _lastMoltenSoundMs;
  #endregion

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);

    // Establish the structure angle up front so GetGlobalPos resolves peripherals on the first
    // production tick (which can fire before the slower completion tick).
    UpdateStructureRotation();

    _toggle = new ToggleAnimator(this, BuildAnimator);
    _toggle.Initialize(ApplyControlPose);
  }

  // Non-RCC animated block: loads the shape and initialises the animator through the shared toggle
  // helper, which owns the null-animator ready-guard. A failed shape resolve leaves it not ready.
  private void BuildAnimator(BEBehaviorAnimatable animatable) {
    var capi = (ICoreClientAPI)Api;
    // Not mesh-cached: InitializeAnimator resolves joints into the shape it is handed, so each animator
    // needs its own instance. Shape.TryGet parses afresh per call.
    if (
      ExMeshCache.LoadShape(capi, ExMeshCache.ShapePathOf(Block))
      is not { } shape
    )
      return;

    // Rotation is applied only by the renderer, not baked into the mesh: InitializeShapeAnd-
    // Animator does both and would rotate the control 180° off.
    animatable.animUtil.InitializeAnimator(
      "bessemercontrol-" + Block.Variant["side"],
      shape,
      capi.Tesselator.GetTextureSource(Block),
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
  }

  #endregion

  #region Production tick (server only, started by base when StructureComplete)

  protected override void OnProductionTick(float dt) {
    if (!StructureComplete || !IsConverterConstructed()) {
      SetStatus(Lang.Get("smex:bessemer-status-notbuilt"));
      return;
    }

    if (!IsGasIntakeAligned()) {
      SetStatus(Lang.Get("smex:bessemer-status-misaligned"));
      return;
    }

    if (!IsTransmissionAligned()) {
      SetStatus(Lang.Get("smex:bessemer-status-transmission-misaligned"));
      return;
    }

    UpdateSolidified();
    SyncContentCooldown();

    switch (OpState) {
      case ConverterOpState.Filling:
        TickFilling(dt);
        break;
      case ConverterOpState.SlagPouring:
        TickSlagPouring(dt);
        break;
      case ConverterOpState.SteelPouring:
        TickSteelPouring(dt);
        break;
      default:
        TickNormal(dt);
        break;
    }
  }

  private void TickNormal(float dt) {
    // Idles while holding the charge; runs the autothermal blow while the charge is blowable.
    if (_charge == null || _charge.Units <= 0) {
      SetStatus(Lang.Get("smex:bessemer-status-empty"));
      return;
    }

    if (_solidified) {
      SetStatus(SolidifiedStatus());
      return;
    }

    // Only pig and Bessemer steel take the blow. An over-blown iron heat, or any foreign metal,
    // waits to be poured.
    if (!IsBlowable()) {
      SetStatus(
        Lang.Get(
          IsOverblownIron()
            ? "smex:bessemer-status-ironready"
            : "smex:bessemer-status-foreign"
        )
      );
      return;
    }

    // Draw blast off the gas network first: the amount that actually arrives drives both the heat
    // balance (autothermal T_in scales with it) and the decarburisation this tick.
    float demand = BlastPerSecond * dt;
    float blastConsumed = TryConsumeBlast(demand);
    float airFactor =
      demand > 0f ? GameMath.Clamp(blastConsumed / demand, 0f, 1f) : 0f;

    // Recomputed and stashed every tick, including a paused one, so the HUD always explains the state.
    _lastHeatBalance = ComputeHeatBalance(airFactor);
    MarkDirty();

    if (blastConsumed <= 0f) {
      // With the blast cut, a bath at the steel target is ready to pour or to over-blow once the
      // blast resumes; pig still needs air to finish.
      SetStatus(
        Lang.Get(
          IsBessemerSteel()
            ? "smex:bessemer-status-steelready"
            : "smex:bessemer-status-blow-paused",
          CarbonPercentText()
        )
      );
      return;
    }

    // The blow drives the bath toward its autothermal equilibrium (T_in - T_loss). Cold scrap pulls
    // that equilibrium down; enough of it drops the bath below its melting point and freezes it.
    HoldBathTemperature(_lastHeatBalance.TProcess);

    // Refining requires the bath to clear the refine floor (the steel liquidus); a bath dragged
    // under it by a heavy cold-scrap charge stalls here and cools toward the freeze.
    if (_lastHeatBalance.TProcess < RefineTemperature) {
      SetStatus(
        Lang.Get("smex:bessemer-status-blow-stalled", CarbonPercentText())
      );
      return;
    }

    // Process smoke and blast sound play only while actively refining.
    GetConverter()?.SpawnSmokeParticles();
    ExSounds.PlayThrottled(
      Api,
      Pos.AddCopy(0, 0, 2),
      ExSounds.Embers,
      ref _lastProcessSoundMs,
      4000,
      0.5f
    );
    ExSounds.PlayThrottled(
      Api,
      Pos.AddCopy(0, 0, 2),
      ExSounds.Fire,
      ref _lastFireSoundMs,
      3000,
      1.5f
    );

    BlowStep(blastConsumed);

    SetStatus(BlowingStatus());
    MarkDirty();
  }

  /// <summary>
  /// One blow step: oxidises carbon in proportion to the blast that reached the bath, sheds the
  /// oxidised mass into slag and gas as it goes (mass-conserving), and retypes the charge as carbon
  /// crosses the thresholds - pig to Bessemer steel at target, steel to soft ingot iron on over-blow.
  /// </summary>
  private void BlowStep(float blastConsumed) {
    if (_charge == null)
      return;

    float prevCarbon = _carbon;
    _carbon = Math.Max(0f, _carbon - CarbonPerBlastLitre * blastConsumed);

    if (IsPig()) {
      // Impurities burn off and slag forms across the [target, start] carbon band. Only the part of
      // this tick's carbon drop inside that band sheds mass, so an overshoot never over-sheds.
      float range = Math.Max(1e-6f, PigCarbonStart - SteelCarbonTarget);
      float bandBurned =
        GameMath.Clamp(prevCarbon, SteelCarbonTarget, PigCarbonStart)
        - GameMath.Clamp(_carbon, SteelCarbonTarget, PigCarbonStart);
      if (bandBurned > 0f) {
        // Total mass shed across the whole blow is pigCharged × (1 − steelYield), split into slag
        // and gas by their yields. The carry keeps sub-unit sheds from being rounded away.
        float lossFrac = Math.Max(1e-6f, 1f - SteelYield);
        _shedCarry += _pigCharged * lossFrac * (bandBurned / range);
        int shed = Math.Min((int)_shedCarry, _charge.Units);
        if (shed > 0) {
          _charge.Units -= shed;
          _shedCarry -= shed;
          _moltenSlag += shed * (SlagYield / lossFrac); // the rest of the shed is gas (gone)
        }
      }

      if (_carbon <= SteelCarbonTarget)
        RetypeToSteel();
    } else if (IsBessemerSteel() && _carbon <= OverblowCarbon) {
      // Over-blow: carbon driven to near zero leaves soft, slag-free ingot iron. Mass is unchanged;
      // the carbon that left is already accounted for in the gas.
      _charge.RetypeTo(Api.World, IronCode, ContentCooldownSpeed);
    }
  }

  // Retypes the pig charge to Bessemer steel at the carbon target and melts any cold scrap into it;
  // scrap yields ScrapSteelYield to steel and the rest is lost as gas.
  private void RetypeToSteel() {
    if (
      _charge == null
      || !_charge.RetypeTo(Api.World, SteelCode, ContentCooldownSpeed)
    )
      return;
    int scrapSteel = (int)Math.Round(_scrapUnits * ScrapSteelYield);
    if (scrapSteel > 0)
      _charge.Units += scrapSteel;
    _scrapUnits = 0;
    _shedCarry = 0f;
  }

  private void TickFilling(float dt) {
    if (_solidified) {
      SetStatus(SolidifiedStatus());
      return;
    }

    var inputCell = GetMoltenCell(InputTapLocal);

    // Respect the tap's open/closed state: a closed tap's cell still receives metal from the
    // network, so check it here or the vessel would fill through a shut tap.
    if (inputCell is BlockEntityMoltenCanalTap { IsPouring: false }) {
      SetStatus(Lang.Get("smex:bessemer-status-filling-tapclosed"));
      return;
    }

    if (inputCell == null || !inputCell.HasMoltenMetal) {
      SetStatus(Lang.Get("smex:bessemer-status-filling-nometal"));
      return;
    }

    // Cold scrap counts against the vessel capacity; it is mass sitting in the bath.
    int held = (_charge?.Units ?? 0) + _scrapUnits;
    if (held >= CapacityUnits) {
      SetStatus(Lang.Get("smex:bessemer-status-filling-full"));
      return;
    }

    // Only accept a single metal type at a time.
    if (
      _charge != null
      && _charge.MetalCode.ToString() != inputCell.CellMetalType
    ) {
      SetStatus(Lang.Get("smex:bessemer-status-filling-mismatch"));
      return;
    }

    int space = CapacityUnits - held;
    int toDrain = Math.Min(inputCell.CellAmount, space);
    if (toDrain <= 0)
      return;

    // Capture metal identity/temperature before draining empties the cell.
    string type = inputCell.CellMetalType;
    float temp = inputCell.CellTemperature;

    float drained = inputCell.DrainMetal(toDrain);
    if (drained <= 0f)
      return;

    _charge ??= MoltenCharge.Create(
      Api.World,
      type,
      temp,
      0,
      ContentCooldownSpeed
    );
    if (_charge == null)
      return;

    _charge.SetTemperature(Api.World, temp);
    _charge.Units += (int)drained;

    // A pig fill reseeds the carbon for the blow: fresh pig arrives at PigCarbonStart, so a top-up
    // onto a partly-blown bath raises the mass-weighted average and grows the pig-mass yield basis.
    if (_charge.MetalCode.ToString() == PigCode) {
      int pigUnits = _charge.Units;
      _carbon =
        pigUnits > 0
          ? (
            _carbon * (pigUnits - (int)drained) + PigCarbonStart * (int)drained
          ) / pigUnits
          : PigCarbonStart;
      _pigCharged += (int)drained;
    }

    ExSounds.PlayThrottled(
      Api,
      Pos,
      ExSounds.Sizzle,
      ref _lastMoltenSoundMs,
      1500,
      0.6f
    );
    SetStatus(
      Lang.Get(
        "smex:bessemer-status-filling",
        held + (int)drained,
        CapacityUnits
      )
    );
    MarkDirty();
  }

  // Shallow tilt: skims the floating slag off the top through the shared output cell. Slag is a
  // small fraction of the steel, so it drains in seconds before the tilt moves on to the steel pour.
  private void TickSlagPouring(float dt) {
    if (_solidified) {
      SetStatus(SolidifiedStatus());
      return;
    }

    if (_moltenSlag <= 0f) {
      SetStatus(Lang.Get("smex:bessemer-status-slag-empty"));
      return;
    }

    var outputCell = GetMoltenCell(OutputStartLocal);
    if (outputCell == null) {
      SetStatus(Lang.Get("smex:bessemer-status-pouring-nocanal"));
      return;
    }

    int amount = Math.Min((int)Math.Ceiling(_moltenSlag), PourPerTick(dt));
    float bathTemp = _charge?.Temperature(Api.World) ?? RefineTemperature;
    ItemStack? slagStack = MoltenMetal.CreateStack(
      Api.World,
      SlagCode,
      bathTemp,
      ContentCooldownSpeed
    );
    if (slagStack == null)
      return;

    float accepted = outputCell.PushMetal(amount, slagStack, Api.World);
    if (accepted <= 0f) {
      // Output cell full or carrying steel: keep it hot so it stays molten and report the block.
      // The single-medium canal guard is what keeps slag and steel apart.
      outputCell.SoakHeat(Api.World, bathTemp);
      SetStatus(Lang.Get("smex:bessemer-status-slag-blocked"));
      return;
    }

    _moltenSlag -= accepted;
    ExSounds.PlayThrottled(
      Api,
      Pos,
      ExSounds.MoltenMetal,
      ref _lastMoltenSoundMs,
      1500,
      0.6f
    );
    SetStatus(Lang.Get("smex:bessemer-status-slag-pouring", (int)_moltenSlag));
    MarkDirty();
  }

  // Deep tilt: pours the steel beneath the slag out through the same output cell.
  private void TickSteelPouring(float dt) {
    if (_charge == null || _charge.Units <= 0) {
      SetStatus(Lang.Get("smex:bessemer-status-pouring-empty"));
      return;
    }

    if (_solidified) {
      SetStatus(SolidifiedStatus());
      return;
    }

    var outputCell = GetMoltenCell(OutputStartLocal);
    if (outputCell == null) {
      SetStatus(Lang.Get("smex:bessemer-status-pouring-nocanal"));
      return;
    }

    int amount = Math.Min(_charge.Units, PourPerTick(dt));
    float accepted = outputCell.PushMetal(amount, _charge.Stack, Api.World);
    if (accepted <= 0f) {
      // Output canal full: keep soaking it with the bath's heat so it stays molten and keeps
      // feeding downstream instead of cooling to a plug. Mirrors the furnace tap's heat soak.
      outputCell.SoakHeat(Api.World, _charge.Temperature(Api.World));
      SetStatus(Lang.Get("smex:bessemer-status-pouring-full"));
      return;
    }

    _charge.Units -= (int)accepted;
    ExSounds.PlayThrottled(
      Api,
      Pos,
      ExSounds.MoltenMetal,
      ref _lastMoltenSoundMs,
      1500,
      0.6f
    );
    if (_charge.Units <= 0) {
      _charge = null;
      // The steel is out; clear the heat's carbon, pig and scrap bookkeeping. Un-poured slag stays
      // until it too is drained by a slag-pour tick.
      _carbon = 0f;
      _pigCharged = 0;
      _scrapUnits = 0;
      _shedCarry = 0f;
      SetStatus(Lang.Get("smex:bessemer-status-emptied"));
    } else {
      SetStatus(
        Lang.Get("smex:bessemer-status-pouring", _charge.Units, CapacityUnits)
      );
    }
    MarkDirty();
  }

  private static int PourPerTick(float dt) => Math.Max(1, (int)(PourRate * dt));

  #endregion

  #region Cold scrap charge (the temperature gate)

  /// <summary>
  /// Charges cold steel scrap from the player's hotbar into the vessel - any item carrying the exlib
  /// <see cref="Roles.Scrap"/> role, such as <c>game:metalbit-steel</c>. Scrap is cold mass that
  /// raises the bath's heat loss until it melts into the steel at the carbon target.
  /// </summary>
  /// <returns><c>false</c> when the held item is not scrap, leaving the click to fall through to
  /// state selection; <c>true</c> otherwise, with <paramref name="error"/> set when refused.</returns>
  public bool TryChargeScrap(IPlayer byPlayer, out string error) {
    error = "";
    ItemStack? held = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
    if (held == null || !MaterialRoleRegistry.IsRole(Roles.Scrap, held))
      return false; // not scrap - the click falls through

    if (!CanOperate(out error))
      return true;
    if (_solidified) {
      error = Lang.Get("smex:bessemer-err-scrap-solidified");
      return true;
    }
    // Scrap goes into an empty vessel or a raw pig charge, before the blow makes steel; adding it to
    // a finished heat would leave unrefined cold lumps in the steel.
    if (_charge != null && _charge.MetalCode.ToString() != PigCode) {
      error = Lang.Get("smex:bessemer-err-scrap-notpig");
      return true;
    }

    int held0 = (_charge?.Units ?? 0) + _scrapUnits;
    int roomBits = (CapacityUnits - held0) / Math.Max(1, ScrapUnitValue);
    if (roomBits <= 0) {
      error = Lang.Get("smex:bessemer-status-filling-full");
      return true;
    }

    if (Api.Side != EnumAppSide.Server)
      return true;

    int taken = ExInventory.TakeHotbar(
      byPlayer,
      s => MaterialRoleRegistry.IsRole(Roles.Scrap, s),
      roomBits
    );
    if (taken <= 0)
      return true;

    _scrapUnits += taken * ScrapUnitValue;
    ExSounds.Play(Api, Pos.AddCopy(0, 0, 2), ExSounds.MetalGrinding, 0.6f);
    SetStatus(Lang.Get("smex:bessemer-status-scrap-charged", _scrapUnits));
    MarkDirty(true);
    return true;
  }

  #endregion

  #region Temperature handling

  // Re-stamps the live charge's cooldown rate from config every tick, so a config change reaches
  // metal already in the vessel, not only metal added on a later fill. The cooldown baseline is
  // rebased to the current temperature (see MoltenMetal.SyncCooldownSpeed) so the new rate applies
  // from this tick forward even for an idle charge.
  private void SyncContentCooldown() {
    _charge?.SyncCooldown(Api.World, ContentCooldownSpeed);
  }

  // Drives the bath to the autothermal equilibrium while blowing, in both directions: up as the blow
  // heats it, down when a cold-scrap charge makes T_loss exceed T_in. Never a fixed value.
  private void HoldBathTemperature(float tProcess) {
    if (_charge == null)
      return;
    _charge.SetTemperature(
      Api.World,
      Math.Max(ExlibValues.AmbientTemperature, tProcess)
    );
  }

  /// <summary>
  /// The converter's autothermal heat balance, <c>T_process = T_in - T_loss</c>, through the shared
  /// exlib helper and with no maximum temperature. <c>T_in</c> is the pig's own oxidation heat scaled
  /// by how much blast reached the bath; <c>T_loss</c> is radiation plus the cold mass of any scrap.
  /// Enough scrap drags T_process under the refine floor (a stall), then under the melting point.
  /// </summary>
  private HeatBalance ComputeHeatBalance(float airFactor) {
    float tIn = Math.Min(
      AutothermalCeiling,
      AutothermalBase + HeatPerCarbonUnit * airFactor
    );
    float chargeLoss = ColdScrapLossCoefficient * _scrapUnits;
    float tLoss = RadiationLoss + chargeLoss;

    // fuelFrac, fuelFactor and preheat are the furnace's coke contributors; the converter has none,
    // so they stay at zero/one. Ambient as the reference temperature gives no ambient loss.
    return HeatBalance.Compute(
      tIn,
      tLoss,
      ExlibValues.AmbientTemperature,
      fuelFrac: 0f,
      fuelFactor: 1f,
      airFactor: airFactor,
      blastSupplied: airFactor > 0f,
      blastTemp: ExlibValues.AmbientTemperature,
      preheatGain: 0f,
      chargeLoss: chargeLoss,
      ambientLoss: 0f
    );
  }

  private void UpdateSolidified() {
    if (_charge == null || _charge.Units <= 0) {
      if (_solidified) {
        _solidified = false;
        SyncConverter();
      }
      return;
    }

    bool nowSolid = _charge.IsBelowMeltingPoint(Api.World);
    if (nowSolid != _solidified) {
      _solidified = nowSolid;
      if (nowSolid)
        ExSounds.Play(Api, Pos, ExSounds.Extinguish, 0.7f);
      SyncConverter();
      MarkDirty();
    }
  }

  #endregion

  #region Content queries

  // The bath is molten pig, still blowing toward steel.
  private bool IsPig() =>
    _charge != null
    && _charge.Units > 0
    && _charge.MetalCode.ToString() == PigCode;

  // The bath is Bessemer steel, at the carbon target and still over-blowable to iron.
  private bool IsBessemerSteel() =>
    _charge != null
    && _charge.Units > 0
    && _charge.MetalCode.ToString() == SteelCode;

  // The bath is over-blown soft ingot iron, the terminal near-zero-carbon product of the blow.
  private bool IsOverblownIron() =>
    _charge != null
    && _charge.Units > 0
    && _charge.MetalCode.ToString() == IronCode;

  // Pig blows to steel; steel can be over-blown to iron. Both take the blast; iron/foreign do not.
  private bool IsBlowable() =>
    (IsPig() || IsBessemerSteel()) && _charge!.IsLiquid(Api.World);

  private float CarbonPercent() => Math.Max(0f, _carbon) * 100f;

  private string CarbonPercentText() => $"{CarbonPercent():0.00}%";

  // Names the phase the carbon has reached, so the stop can be timed against it.
  private string BlowingStatus() =>
    Lang.Get(
      IsBessemerSteel()
        ? "smex:bessemer-status-overblowing"
        : "smex:bessemer-status-blowing",
      CarbonPercentText()
    );

  #endregion

  #region Player-driven state transitions

  /// <summary>
  /// True when the operating mode can be changed: structure complete, vessel constructed,
  /// peripherals aligned and mechanical power present. Otherwise false, with
  /// <paramref name="error"/> set to a player-facing reason.
  /// </summary>
  public bool CanOperate(out string error) {
    error = "";
    if (!StructureComplete) {
      error = Lang.Get("smex:bessemer-err-incomplete");
      return false;
    }
    if (!IsConverterConstructed()) {
      error = Lang.Get("smex:bessemer-err-notbuilt");
      return false;
    }
    if (!IsGasIntakeAligned()) {
      error = Lang.Get("smex:bessemer-err-intake-misaligned");
      return false;
    }
    if (!IsTransmissionAligned()) {
      error = Lang.Get("smex:bessemer-err-transmission-misaligned");
      return false;
    }
    if (!HasPower()) {
      error = Lang.Get("smex:bessemer-err-nopower");
      return false;
    }
    return true;
  }

  /// <summary>
  /// Switches operational state, subject to <see cref="CanOperate"/>. Returns false with
  /// <paramref name="error"/> set to a reason the block can surface to the player.
  /// </summary>
  public bool TrySetState(
    IPlayer byPlayer,
    ConverterOpState newState,
    out string error
  ) {
    if (!CanOperate(out error))
      return false;

    if (OpState == newState)
      return true;

    OpState = newState;
    if (Api.Side == EnumAppSide.Server) {
      // Lever clunk at the control, layered over the vessel grinding round on its trunnions.
      ExSounds.Play(Api, Pos, ExSounds.CokeOvenDoorOpen, 0.9f);
      ExSounds.Play(Api, Pos.AddCopy(0, 0, 2), ExSounds.MetalGrinding, 0.7f);
      SyncConverter();
      MarkDirty(true);
    } else {
      ApplyControlPose();
    }
    return true;
  }

  #endregion

  #region Chisel-out (small hardened residue)

  /// <summary>True when a solidified charge is present (latched below the melting point).</summary>
  public bool HasSolidifiedCharge =>
    _solidified && _charge != null && _charge.Units > 0;

  /// <summary>True when the charge has cooled below the hardened (chisellable) threshold.</summary>
  public bool ChargeIsHardened =>
    _charge != null && _charge.Units > 0 && _charge.IsHardened(Api.World);

  /// <summary>
  /// True when a residue can be chiselled out instead of breaking the converter: the charge must be
  /// solidified, cooled to hardened, and below <see cref="ChiselMaxFraction"/> of capacity.
  /// </summary>
  public bool CanChiselOut() =>
    HasSolidifiedCharge
    && ChargeIsHardened
    && (_charge?.Units ?? 0) < ChiselMaxFraction * CapacityUnits;

  /// <summary>
  /// Chips the hardened residue out of the vessel, clears the charge and returns the recovered
  /// metal-bit drop at full amount, with none of the break loss. Server-side; returns <c>null</c>
  /// on the client or when the charge is not chiselable.
  /// </summary>
  public ItemStack? ChiselOutContent() {
    if (Api?.Side != EnumAppSide.Server || !CanChiselOut())
      return null;

    ItemStack? recovered = BuildRecoveryDrops(_charge?.Units ?? 0);
    _charge = null;
    ResetHeat();
    _solidified = false;
    SyncConverter();
    MarkDirty(true);
    return recovered;
  }

  // Status text for a frozen charge: residue too large to chisel -> break the vessel to salvage it;
  // small but still too hot -> wait for it to harden; small and fully hardened -> chisel it out.
  private string SolidifiedStatus() {
    if ((_charge?.Units ?? 0) >= ChiselMaxFraction * CapacityUnits)
      return Lang.Get("smex:bessemer-status-solidified");

    return Lang.Get(
      ChargeIsHardened
        ? "smex:bessemer-status-chiselout"
        : "smex:bessemer-status-coolingtochisel"
    );
  }

  #endregion

  #region Animation

  private void ApplyControlPose() {
    _toggle?.Pose(util => {
      util.StopAnimation("filling");
      util.StopAnimation("pouring");

      // The lever holds one of two positions: a fill throw, or a pour throw shared by the slag and
      // steel tilts - the depth distinction is on the vessel, not the lever.
      string? code = OpState switch {
        ConverterOpState.Filling => "filling",
        ConverterOpState.SlagPouring or ConverterOpState.SteelPouring =>
          "pouring",
        _ => null,
      };
      if (code != null)
        util.StartAnimation(
          new AnimationMetaData {
            Animation = code,
            Code = code,
            AnimationSpeed = 3.0f, // a lever pull is quick
            EaseInSpeed = 8f,
            EaseOutSpeed = 8f,
          }.Init()
        );
    });
  }

  private void SyncConverter() {
    GetConverter()?.UpdateMirror(_solidified, _charge?.Units ?? 0, OpState);
  }

  protected override void OnStructureCompleted() => SyncConverter();

  protected override void OnStructureLost() {
    if (OpState != ConverterOpState.Normal) {
      OpState = ConverterOpState.Normal;
      ApplyControlPose();
    }
  }

  #endregion

  #region Abstract impls

  protected override void UpdateStructureRotation() {
    if (Block == null)
      return;

    // The control's local frame faces opposite the stored angle: init at angle + 180, matched by
    // GetGlobalPos (the +180 convention).
    SetStructureAngle(
      ExOrientation.AngleFromSide(Block.Variant["side"]),
      initAngleOffset: 180
    );
  }

  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.Get("smex:bessemer-err-incomplete-count", missingCount);

  protected override string GetCompleteMessage() =>
    Lang.Get("smex:bessemer-complete");

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("opState", (int)OpState);
    tree.SetItemstack("content", _charge?.Stack);
    tree.SetInt("contentUnits", _charge?.Units ?? 0);
    tree.SetFloat("carbon", _carbon);
    tree.SetInt("pigCharged", _pigCharged);
    tree.SetFloat("shedCarry", _shedCarry);
    tree.SetInt("scrapUnits", _scrapUnits);
    tree.SetFloat("moltenSlag", _moltenSlag);
    tree.SetBool("solidified", _solidified);
    tree.SetString("status", _status);
    WriteHeatBalance(tree);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    var prevState = OpState;
    OpState = (ConverterOpState)tree.GetInt("opState");
    _charge = MoltenCharge.FromTree(
      tree,
      "content",
      "contentUnits",
      worldForResolving
    );
    _carbon = tree.GetFloat("carbon");
    _pigCharged = tree.GetInt("pigCharged");
    _shedCarry = tree.GetFloat("shedCarry");
    _scrapUnits = tree.GetInt("scrapUnits");
    _moltenSlag = tree.GetFloat("moltenSlag");
    _solidified = tree.GetBool("solidified");
    _status = tree.GetString("status", Lang.Get("smex:bessemer-status-idle"));
    ReadHeatBalance(tree);

    if (Api?.Side == EnumAppSide.Client && prevState != OpState)
      ApplyControlPose();
  }

  // The whole balance rides the tree, not just its result: GetBlockInfo runs client-side, where the
  // bath is never blown and the pipes are never read, so anything the HUD prints must arrive here.
  private void WriteHeatBalance(ITreeAttribute tree) {
    HeatBalance hb = _lastHeatBalance;
    tree.SetFloat("hbIn", hb.TIn);
    tree.SetFloat("hbLoss", hb.TLoss);
    tree.SetFloat("hbProcess", hb.TProcess);
    tree.SetFloat("hbAirFactor", hb.AirFactor);
    tree.SetBool("hbBlastSupplied", hb.BlastSupplied);
    tree.SetFloat("hbChargeLoss", hb.ChargeLoss);
  }

  private void ReadHeatBalance(ITreeAttribute tree) {
    _lastHeatBalance = new HeatBalance(
      tree.GetFloat("hbIn"),
      tree.GetFloat("hbLoss"),
      tree.GetFloat("hbProcess", 20f),
      FuelFrac: 0f,
      FuelFactor: 1f,
      tree.GetFloat("hbAirFactor"),
      tree.GetBool("hbBlastSupplied"),
      BlastTemp: ExlibValues.AmbientTemperature,
      PreheatGain: 0f,
      tree.GetFloat("hbChargeLoss"),
      AmbientLoss: 0f
    );
  }

  /// <summary>Maps the bath's carrier stack, so a converter mid-heat pasted into another world still
  /// resolves the molten metal against that world's item ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) =>
    _charge?.Stack.Collectible?.OnStoreCollectibleMappings(
      Api.World,
      new DummySlot(_charge.Stack),
      blockIdMapping,
      itemIdMapping
    );

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    // A false return means the destination world has no such item/block; FixMapping leaves Id at the
    // source world's value, which would resolve to whatever owns that id there. Null the whole charge
    // instead of keeping a mis-resolved one - MoltenCharge.Stack is never null while a charge exists,
    // matching vanilla's BEIngotMold.cs:806-809.
    if (
      _charge?.Stack.FixMapping(
        oldBlockIdMapping,
        oldItemIdMapping,
        worldForResolve
      ) == false
    )
      _charge = null;
  }

  #endregion
}
