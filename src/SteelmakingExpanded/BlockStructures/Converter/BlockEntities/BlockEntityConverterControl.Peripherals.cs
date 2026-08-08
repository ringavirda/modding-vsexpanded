using System;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace SteelmakingExpanded.BlockStructures.Converter.BlockEntities;

// Peripheral side of the control: resolving the structure-local offsets to world cells,
// drawing blast/power through them, spawning the vessel, and the vessel-break handoff.
public partial class BlockEntityConverterControl {
  #region Peripheral access
  // The converter's local frame faces opposite the structure angle, so peripherals map at
  // _currentAngle + 180 (see the +180 convention).
  protected override BlockPos GetGlobalPos(
    int localX,
    int localY,
    int localZ
  ) =>
    ExOrientation.GlobalPos(
      Pos,
      localX,
      localY,
      localZ,
      (_currentAngle + 180) % 360
    );

  private BlockPos PeripheralPos((int x, int y, int z) local) =>
    GetGlobalPos(local.x, local.y, local.z);

  private BlockEntityMoltenCanal? GetMoltenCell((int x, int y, int z) local) =>
    Api.World.BlockAccessor.GetBlockEntity(PeripheralPos(local))
    as BlockEntityMoltenCanal;

  private float TryConsumeBlast(float amount) {
    // The intake is a fixed connector, not a node - the blast network lives in the cell across
    // its connector face, not in the intake cell.
    BlockPos intakePos = PeripheralPos(GasIntakeLocal);
    if (
      Api.World.BlockAccessor.GetBlock(intakePos)
      is not Blocks.BlockConverterIntake intake
    )
      return 0f;
    // Only draw blast from a network whose pipe presents a connector back at the intake face.
    if (
      this.NetworkSystem()
        ?.GetConnectedNetworkAcross(
          Api.World.BlockAccessor,
          intakePos,
          intake.ConnectorFace
        )
      is not PipeNetwork pipeNet
    )
      return 0f;
    // Blast is air at or above the blast threshold pressure (3 atm).
    if (
      pipeNet.State?.MediumType != "Air"
      || pipeNet.State.Pressure < SmexValues.BlastPressureThreshold
    )
      return 0f;
    return pipeNet.TryConsumeGas(amount, Api.World.BlockAccessor);
  }

  /// <summary>True if the transmission's mechanical network is turning.</summary>
  public bool HasPower() {
    var be = Api.World.BlockAccessor.GetBlockEntity(
      PeripheralPos(TransmissionLocal)
    );
    if (
      be?.GetBehavior<BEBehaviorMPBase>() is not BEBehaviorMPBase mp
      || mp.Network == null
    )
      return false;
    return Math.Abs(mp.Network.Speed * mp.GearedRatio) > PowerSpeedThreshold;
  }

  private BlockEntityConverterBessemer? GetConverter() =>
    Api.World.BlockAccessor.GetBlockEntity(PeripheralPos(ConverterLocal))
    as BlockEntityConverterBessemer;

  /// <summary>True if the converter vessel block has been placed at its structure offset.</summary>
  public bool IsConverterPresent() =>
    Api.World.BlockAccessor.GetBlock(PeripheralPos(ConverterLocal))
    is Blocks.BlockConverterBessemer;

  /// <summary>True if the converter vessel has finished its right-click construction stages.</summary>
  public bool IsConverterConstructed() =>
    GetConverter()?.IsConstructed ?? false;

  /// <summary>
  /// The gas intake must face the same way as the control (matching <c>side</c> variant) or its
  /// blast connector won't line up. The multiblock check accepts any orientation, so validate here.
  /// </summary>
  public bool IsGasIntakeAligned() {
    Block intake = Api.World.BlockAccessor.GetBlock(
      PeripheralPos(GasIntakeLocal)
    );
    if (intake is not Blocks.BlockConverterIntake)
      return false;

    return intake.Variant["side"] == (Block.Variant["side"] ?? "north");
  }

  /// <summary>
  /// The transmission must face the same way as the control (matching <c>side</c> variant) or its
  /// axle connector will not line up. The multiblock check accepts any orientation.
  /// </summary>
  public bool IsTransmissionAligned() {
    Block trans = Api.World.BlockAccessor.GetBlock(
      PeripheralPos(TransmissionLocal)
    );
    if (trans is not Blocks.BlockConverterTransmission)
      return false;

    return trans.Variant["side"] == (Block.Variant["side"] ?? "north");
  }

  #endregion

  #region Converter spawning

  /// <summary>
  /// Spawns the converter block in the correct cell/orientation, consuming the
  /// gears and rods from the player's hotbar. Returns false (with reason) if the
  /// converter already exists, the cell is blocked, or materials are missing.
  /// </summary>
  public bool TrySpawnConverter(IPlayer byPlayer, out string error) {
    error = "";
    if (IsConverterPresent()) {
      error = Lang.Get("smex:bessemer-err-converter-present");
      return false;
    }

    if (GetConverterBlock() is not Block converter)
      return false;

    BlockPos pos = PeripheralPos(ConverterLocal);
    Block existing = Api.World.BlockAccessor.GetBlock(pos);
    if (existing.Id != 0 && !existing.IsReplacableBy(converter)) {
      error = Lang.Get("smex:bessemer-err-converter-blocked");
      return false;
    }

    // The converter renders across a 3x3x3 volume reserved with invisible fillers; check every
    // cell is free first.
    int fillerAngle = ExOrientation.AngleFromSide(converter.Variant["side"]);
    var fillerCells = StructureFillers.FootprintCells(
      (IFillerHost)converter,
      pos,
      fillerAngle
    );
    if (!StructureFillers.CanPlace(Api.World, fillerCells)) {
      error = Lang.Get("smex:bessemer-err-converter-blocked");
      return false;
    }

    // Creative builders get the vessel for free, with no gears or rods consumed.
    bool isCreative =
      byPlayer.WorldData?.CurrentGameMode == EnumGameMode.Creative;

    if (!isCreative && !HasSpawnMaterials(byPlayer)) {
      error = Lang.Get(
        "smex:bessemer-err-materials",
        SmexValues.BessemerRequiredGears,
        SmexValues.BessemerRequiredRods
      );
      return false;
    }

    if (Api.Side != EnumAppSide.Server)
      return true;

    Api.World.BlockAccessor.SetBlock(converter.BlockId, pos);
    if (
      Api.World.BlockAccessor.GetBlockEntity(pos)
      is BlockEntityConverterBessemer be
    )
      be.LinkControl(Pos);

    // Reserve the volume with fillers pointing back at the converter; breaking any breaks it.
    StructureFillers.PlaceFillers(Api.World, pos, fillerCells);

    if (!isCreative)
      ConsumeSpawnMaterials(byPlayer);
    return true;
  }

  private Block? GetConverterBlock() {
    string side = Block.Variant["side"];
    return Api.World.GetBlock(
      new AssetLocation("smex:" + BlockConverterBessemer.BaseCode + "-" + side)
    );
  }

  // Spawn materials are taken from the hotbar only, unlike engine repairs. The gear must be a
  // smithable iron or steel large gear, so the vessel stays buildable in worlds where looted rusty
  // gears cannot be obtained.
  private static bool IsSpawnGear(ItemStack stack) =>
    stack.Collectible?.Code?.ToString()
      is "lpex:largegear-iron"
        or "lpex:largegear-steel";

  private static bool IsSpawnRod(ItemStack stack) =>
    stack.Collectible?.Code?.ToString() is "game:rod-iron" or "game:rod-steel";

  private bool HasSpawnMaterials(IPlayer byPlayer) =>
    ExInventory.CountHotbar(byPlayer, IsSpawnGear)
      >= SmexValues.BessemerRequiredGears
    && ExInventory.CountHotbar(byPlayer, IsSpawnRod)
      >= SmexValues.BessemerRequiredRods;

  private void ConsumeSpawnMaterials(IPlayer byPlayer) {
    ExInventory.TakeHotbar(
      byPlayer,
      IsSpawnGear,
      SmexValues.BessemerRequiredGears
    );
    ExInventory.TakeHotbar(
      byPlayer,
      IsSpawnRod,
      SmexValues.BessemerRequiredRods
    );
  }

  #endregion

  #region Converter break handoff

  /// <summary>
  /// Called by the converter block when it is broken. Returns the solidified
  /// drops (bits/slag) to scatter, and clears the charge regardless.
  /// </summary>
  public ItemStack? OnConverterBroken() {
    ItemStack? drops = null;
    if (_solidified && _charge != null && _charge.Units > 0)
      drops = BuildSolidifiedDrops();

    _charge = null;
    ResetHeat();
    _solidified = false;
    OpState = ConverterOpState.Normal;
    MarkDirty(true);
    return drops;
  }

  private ItemStack? BuildSolidifiedDrops() {
    // Breaking the vessel mangles part of the charge: 0, 5 or 10 units less than chiselling would
    // recover.
    int units = _charge?.Units ?? 0;
    int randLoss = Random.Shared.Next(3) * 5;
    int remaining = units - randLoss;
    if (remaining <= 0)
      remaining = units;
    return BuildRecoveryDrops(remaining);
  }

  /// <summary>
  /// Builds the metal-bit recovery stack for <paramref name="units"/> of the current charge (5 units
  /// per bit), carrying the charge temperature; falls back to slag for a non-metal charge. Shared with
  /// the canal/barrel chisel drops via <see cref="MoltenChisel.BuildRecovery"/>.
  /// </summary>
  private ItemStack? BuildRecoveryDrops(int units) =>
    _charge?.BuildRecovery(Api.World, units, slagFallback: true);

  // Clears the whole heat's bookkeeping (carbon, pig basis, cold scrap, slag) - used when the vessel is
  // emptied by breaking or chiselling.
  private void ResetHeat() {
    _carbon = 0f;
    _pigCharged = 0;
    _scrapUnits = 0;
    _shedCarry = 0f;
    _moltenSlag = 0f;
  }

  #endregion
}
