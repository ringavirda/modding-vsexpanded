using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnace;
using IronworkingExpanded.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.BlastFurnace.BlockEntities;

/// <summary>
/// Block entity for the blast furnace multiblock. A <see cref="BlockEntityFurnaceCore"/> whose
/// charge is hearth blast-mix coal piles and whose products are molten iron and slag drained into
/// the iron/slag taps; on extinguish the molten iron solidifies into the hearth and the piles
/// convert to slag. The firing/melting orchestration, timers, serialization, and HUD frame live in
/// the core; this subclass supplies the blast-furnace specifics.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnace : BlockEntityFurnaceCore
{
  private float _moltenIron = 0;
  private float _moltenSlag = 0;

  // Blast-furnace product/charge tunables, re-read each tick (live config) alongside the core's.
  private float _ironPerMeltCycle;
  private float _slagPerMeltCycle;
  private int _blastMixPerMeltCycle;
  private float _maxMoltenIron;
  private float _maxMoltenSlag;

  #region Tunables

  protected override float NaturalMaxTemp => IwexValues.BfNaturalMaxTemp;
  protected override float BoostedMaxTemp => IwexValues.BfBoostedMaxTemp;
  protected override float BlastBoostThreshold => IwexValues.BfBlastBoostThreshold;
  protected override float MeltingPoint => IwexValues.BfIronMeltingPoint;
  protected override int MaxFuelBurnTime => IwexValues.BfMaxFuelBurnTime;
  protected override float MeltStartDelay => IwexValues.BfMeltStartDelay;
  protected override float MeltIntervalSec => IwexValues.BfMeltIntervalSec;
  protected override float TuyereIntakeVolume => IwexValues.TuyereIntakeVolume;
  protected override float BlastPressureThreshold => IwexValues.BlastPressureThreshold;
  protected override int BlastMixRequiredToFire => IwexValues.BlastMixRequiredToFire;

  #endregion

  #region Initialization

  /// <summary>
  /// Re-reads the blast-furnace product/charge tunables alongside the core's, so a live
  /// <c>/exmod config iwex ...</c> change applies on the next tick. The core invokes this (virtually)
  /// at init and at the top of every production tick.
  /// </summary>
  protected override void CacheAttributes()
  {
    base.CacheAttributes();
    _ironPerMeltCycle = IwexValues.BfIronPerMeltCycle;
    _slagPerMeltCycle = IwexValues.BfSlagPerMeltCycle;
    _blastMixPerMeltCycle = IwexValues.BfBlastMixPerMeltCycle;
    _maxMoltenIron = IwexValues.BfMaxMoltenIron;
    _maxMoltenSlag = IwexValues.BfMaxMoltenSlag;
  }

  #endregion

  #region Molten stack construction

  private ItemStack? CreateMoltenStack(string metalCode, int units, float temp)
  {
    // Use the item codes the molten network/molds expect downstream: iron as game:ingot-iron,
    // slag as iwex:slag.
    AssetLocation loc =
      metalCode == "slag"
        ? new AssetLocation("iwex", "slag")
        : new AssetLocation("game", $"ingot-{metalCode}");

    Item? item = Api.World.GetItem(loc);
    if (item == null)
      return null;

    var stack = new ItemStack(item, units);
    item.SetTemperature(Api.World, stack, temp, false);
    return stack;
  }

  #endregion

  #region Outlet/tuyere scan

  protected override void ScanForOutlets()
  {
    _gasOutlets = [GetGlobalPos(0, 3, 1), GetGlobalPos(0, 3, 3)];
    _tuyeres = [GetGlobalPos(0, -2, 1), GetGlobalPos(0, -2, 3)];
  }

  #endregion

  #region Charge hooks

  /// <summary>
  /// Walks the 3×7×3 hearth region once and returns every coal-pile BE, so all per-tick pile
  /// reads/writes share one walk.
  /// </summary>
  private List<(BlockPos pos, BlockEntityCoalPile pile)> CollectHearthPiles()
  {
    var piles = new List<(BlockPos, BlockEntityCoalPile)>();
    BlockPos centerHearth = GetGlobalPos(0, 0, 2);
    Api.World.BlockAccessor.WalkBlocks(
      centerHearth.AddCopy(-1, -3, -1),
      centerHearth.AddCopy(1, 3, 1),
      (block, x, y, z) =>
      {
        if (block.Code?.Path.StartsWith("coalpile") != true)
          return;
        BlockPos pos = new(x, y, z, Pos.dimension);
        if (
          Api.World.BlockAccessor.GetBlockEntity(pos)
          is BlockEntityCoalPile pileBe
        )
          piles.Add((pos, pileBe));
      }
    );
    return piles;
  }

  protected override object CollectCharge() => CollectHearthPiles();

  protected override int ReadChargeMix(object chargeHandle, out bool isFull) =>
    GetBlastMixCount(
      (List<(BlockPos pos, BlockEntityCoalPile pile)>)chargeHandle,
      out isFull
    );

  protected override bool TryIgniteCharge(object chargeHandle)
  {
    CheckHearthBurning(
      (List<(BlockPos pos, BlockEntityCoalPile pile)>)chargeHandle,
      out _,
      out bool allBurning
    );
    return allBurning;
  }

  protected override void SmeltCycle(object chargeHandle) =>
    ConsumeForMelting(
      (List<(BlockPos pos, BlockEntityCoalPile pile)>)chargeHandle,
      _blastMixPerMeltCycle,
      _ironPerMeltCycle,
      _slagPerMeltCycle
    );

  private static void CheckHearthBurning(
    List<(BlockPos pos, BlockEntityCoalPile pile)> piles,
    out bool anyBurning,
    out bool allBurning
  )
  {
    bool any = false;
    bool all = true;
    foreach (var (_, pileBe) in piles)
    {
      if (pileBe.IsBurning)
        any = true;
      else
        all = false;
    }
    anyBurning = any;
    allBurning = all && piles.Count > 0;
  }

  private void ConsumeForMelting(
    List<(BlockPos pos, BlockEntityCoalPile pile)> piles,
    int blastmixToConsume,
    float ironProduced,
    float slagProduced
  )
  {
    int consumed = 0;

    // Consume top-down so upper piles empty first, matching the original drip order.
    piles.Sort((a, b) => b.pos.Y.CompareTo(a.pos.Y));
    foreach (var (pos, pileBe) in piles)
    {
      if (consumed >= blastmixToConsume)
        break;
      if (pileBe.inventory is not { Count: > 0 })
        continue;

      var slot = pileBe.inventory[0];
      if (slot.Empty || slot.Itemstack.Collectible.Code.Path != "blastmix")
        continue;

      int take = System.Math.Min(slot.StackSize, blastmixToConsume - consumed);
      slot.TakeOut(take);
      slot.MarkDirty();
      pileBe.MarkDirty(true);
      consumed += take;
      if (slot.Empty)
        Api.World.BlockAccessor.SetBlock(0, pos);
    }

    _moltenIron = System.Math.Min(_moltenIron + ironProduced, _maxMoltenIron);
    _moltenSlag = System.Math.Min(_moltenSlag + slagProduced, _maxMoltenSlag);
  }

  private int GetBlastMixCount(
    List<(BlockPos pos, BlockEntityCoalPile pile)> piles,
    out bool isFull
  )
  {
    int totalMix = 0;
    foreach (var (_, pileBe) in piles)
    {
      // While lit, the furnace manages and keeps its hearth piles burning.
      if (State != FurnaceState.Idle)
      {
        BlastmixPiles.SetManagedByFurnace(pileBe, true);
        if (!pileBe.IsBurning)
          pileBe.TryIgnite();
      }
      foreach (var slot in pileBe.inventory)
      {
        if (
          !slot.Empty && slot.Itemstack.Collectible.Code.Path.Equals("blastmix")
        )
          totalMix += slot.StackSize;
      }
    }

    isFull = totalMix >= IwexValues.BlastMixRequiredToFire;
    return totalMix;
  }

  #endregion

  #region Products: capacity, drain, residue

  protected override bool LiquidCapacityReached =>
    _moltenIron >= _maxMoltenIron || _moltenSlag >= _maxMoltenSlag;

  protected override void DrainProducts(ref bool dirty)
  {
    DrainIronTap(ref dirty);
    DrainSlagTap(ref dirty);
  }

  private void DrainIronTap(ref bool dirty)
  {
    BlockPos lowerTapPos = GetGlobalPos(2, -2, 2);
    if (
      Api.World.BlockAccessor.GetBlockEntity(lowerTapPos)
        is not BlockEntityBlastFurnaceTap lowerTap
      || !lowerTap.IsPouring
      || _moltenIron <= 0
    )
      return;

    int units = Math.Min(20, (int)_moltenIron);
    ItemStack? ironStack = CreateMoltenStack(
      "iron",
      (int)Math.Ceiling(units * 0.6f),
      _internalTemp
    );
    if (ironStack == null)
      return;

    int accepted = lowerTap.TryPourMetal(ironStack, _internalTemp);
    if (accepted > 0)
    {
      _moltenIron -= accepted;
      dirty = true;
      ExSounds.PlayThrottled(
        Api,
        lowerTapPos,
        ExSounds.MoltenMetal,
        ref _lastTapSoundMs,
        2000,
        0.5f
      );
    }
  }

  private void DrainSlagTap(ref bool dirty)
  {
    BlockPos higherTapPos = GetGlobalPos(-2, -1, 2);
    if (
      Api.World.BlockAccessor.GetBlockEntity(higherTapPos)
        is not BlockEntityBlastFurnaceTap higherTap
      || !higherTap.IsPouring
      || _moltenSlag <= 0
    )
      return;

    int units = Math.Min(20, (int)_moltenSlag);
    ItemStack? slagStack = CreateMoltenStack(
      "slag",
      (int)Math.Ceiling(units * 0.8),
      _internalTemp
    );
    if (slagStack == null)
      return;

    int accepted = higherTap.TryPourMetal(slagStack, _internalTemp);
    if (accepted > 0)
    {
      _moltenSlag -= accepted;
      dirty = true;
      ExSounds.PlayThrottled(
        Api,
        higherTapPos,
        ExSounds.MoltenMetal,
        ref _lastTapSoundMs,
        2000,
        0.5f
      );
    }
  }

  protected override void ExtinguishResidue()
  {
    if (_moltenIron > 0)
    {
      Block? solidIronBlock = Api.World.GetBlock(
        new AssetLocation("iwex", "solidifiediron")
      );
      if (solidIronBlock != null)
      {
        int totalNuggets = System.Math.Max(
          1,
          (int)System.Math.Floor(_moltenIron / 5f)
        );
        int nuggets1 =
          totalNuggets < 3 ? 1 : Api.World.Rand.Next(1, totalNuggets - 1);
        int nuggets2 = totalNuggets - nuggets1;

        BlockPos pos1 = GetGlobalPos(0, -2, 2);
        BlockPos pos2 = GetGlobalPos(1, -2, 2);

        Api.World.BlockAccessor.SetBlock(solidIronBlock.BlockId, pos1);
        if (
          Api.World.BlockAccessor.GetBlockEntity(pos1)
          is BlockEntitySolidifiedIron be1
        )
        {
          be1.IronCount = nuggets1;
          be1.MarkDirty(true);
        }

        Api.World.BlockAccessor.SetBlock(solidIronBlock.BlockId, pos2);
        if (
          Api.World.BlockAccessor.GetBlockEntity(pos2)
          is BlockEntitySolidifiedIron be2
        )
        {
          be2.IronCount = nuggets2;
          be2.MarkDirty(true);
        }
      }
    }

    _moltenIron = 0;
    _moltenSlag = 0;

    BlockPos centerHearth = GetGlobalPos(0, 0, 2);
    Api.World.BlockAccessor.WalkBlocks(
      centerHearth.AddCopy(-1, -3, -1),
      centerHearth.AddCopy(1, 3, 1),
      (block, x, y, z) =>
      {
        if (block.Code?.Path.StartsWith("coalpile") == true)
        {
          BlockPos pos = new BlockPos(x, y, z, Pos.dimension);
          if (
            Api.World.BlockAccessor.GetBlockEntity(pos)
            is BlockEntityCoalPile pileBe
          )
          {
            BlastmixPiles.SetManagedByFurnace(pileBe, false);
            BlastmixPiles.ConvertToSlag(pileBe);
          }
        }
      }
    );
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  )
  {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _moltenIron = tree.GetFloat("moltenIron", 0f);
    _moltenSlag = tree.GetFloat("moltenSlag", 0f);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetFloat("moltenIron", _moltenIron);
    tree.SetFloat("moltenSlag", _moltenSlag);
  }

  #endregion

  #region HUD product lines

  protected override void AppendProductInfo(StringBuilder sb)
  {
    sb.AppendLine(
      Lang.Get("iwex:bf-info-molteniron", _moltenIron, _maxMoltenIron)
    );
    sb.AppendLine(
      Lang.Get("iwex:bf-info-moltenslag", _moltenSlag, _maxMoltenSlag)
    );
  }

  protected override void AppendReadyInfo(StringBuilder sb)
  {
    CheckHearthBurning(
      CollectHearthPiles(),
      out bool anyBurning,
      out bool allBurning
    );
    sb.AppendLine(
      Lang.Get(
        anyBurning && !allBurning
          ? "iwex:bf-info-partiallylit"
          : "iwex:bf-info-ready"
      )
    );
  }

  #endregion
}
