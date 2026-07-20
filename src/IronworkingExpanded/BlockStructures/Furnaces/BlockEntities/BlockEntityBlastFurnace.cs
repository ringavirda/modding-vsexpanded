using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using IronworkingExpanded.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The blast furnace: a <see cref="BlockEntityFurnaceCore"/> charged with burden piles in its shaft,
/// producing molten iron and slag through the iron and slag taps.
/// <para>
/// There is exactly one blast furnace in the mod. The cold and the hot furnace are the same machine
/// running under different conditions, so they are the same class, and the difference between them is
/// data: the hot furnace's layout carries exhaust outlets the cold one has no cell for, and a cowper
/// on its blast line arrives preheated at the tuyeres. Everything a player would call "the hot blast
/// furnace" - clearing iron's melt line on a leaner burden, and rendering it faster once it does -
/// falls out of <see cref="BlockEntityFurnaceCore.ComputeHeatBalance"/> reading those two facts. The
/// subclasses below exist to be distinct registered types with distinct layouts, not distinct
/// behaviour; the previous pair of byte-identical copies is what this replaces.
/// </para>
/// </summary>
public abstract class BlockEntityBlastFurnace : BlockEntityFurnaceCore
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

  protected override float MeltingPoint => IwexValues.BfIronMeltingPoint;
  protected override int MaxFuelBurnTime => IwexValues.BfMaxFuelBurnTime;
  protected override float MeltStartDelay => IwexValues.BfMeltStartDelay;
  protected override float MeltIntervalSec => IwexValues.BfMeltIntervalSec;
  protected override float TuyereIntakeVolume => IwexValues.TuyereIntakeVolume;
  protected override float BlastPressureThreshold =>
    IwexValues.BlastPressureThreshold;
  protected override int BlastMixRequiredToFire =>
    IwexValues.BlastMixRequiredToFire;

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
    // The molten network/molds expect the registry's item code per metal (iron game:ingot-iron, slag
    // iwex:slag). MetalRegistry.MoltenItemOf resolves the short token, falling back to the
    // game:ingot-<code> convention for a metal that ships no explicit entry.
    AssetLocation loc = MetalRegistry.MoltenItemOf(metalCode);

    Item? item = Api.World.GetItem(loc);
    if (item == null)
      return null;

    var stack = new ItemStack(item, units);
    item.SetTemperature(Api.World, stack, temp, false);
    return stack;
  }

  #endregion

  #region Charge hooks

  /// <summary>
  /// True for anything the shaft counts as charge: prepared burden, or the legacy count-only blast
  /// mix that predates it. Both still sit in hearth coal piles; only burden carries a composition.
  /// </summary>
  private static bool IsCharge(ItemStack? stack) =>
    Burden.Is(stack) || stack?.Collectible?.Code?.Path == "blastmix";

  /// <summary>
  /// Walks the shaft once and returns every coal-pile block entity in it, so all per-tick pile
  /// reads/writes share one walk. The box comes off the declared shaft bounds rather than a literal
  /// offset, so a furnace with a different column overrides one property instead of this method.
  /// </summary>
  private List<(BlockPos pos, BlockEntityCoalPile pile)> CollectHearthPiles()
  {
    var piles = new List<(BlockPos, BlockEntityCoalPile)>();
    var (min, max) = ShaftBounds();
    Api.World.BlockAccessor.WalkBlocks(
      min,
      max,
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

  protected override int ReadChargeMix(
    object chargeHandle,
    out bool isFull,
    out BurdenMix mix
  ) =>
    GetBlastMixCount(
      (List<(BlockPos pos, BlockEntityCoalPile pile)>)chargeHandle,
      out isFull,
      out mix
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

  protected override IEnumerable<(
    BlockPos pos,
    BlockEntityCoalPile pile
  )> EnumerateChargePiles() => CollectHearthPiles();

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
      if (slot.Empty || !IsCharge(slot.Itemstack))
        continue;

      int take = Math.Min(slot.StackSize, blastmixToConsume - consumed);
      slot.TakeOut(take);
      slot.MarkDirty();
      pileBe.MarkDirty(true);
      consumed += take;
      if (slot.Empty)
        Api.World.BlockAccessor.SetBlock(0, pos);
    }

    _moltenIron = Math.Min(_moltenIron + ironProduced, _maxMoltenIron);
    _moltenSlag = Math.Min(_moltenSlag + slagProduced, _maxMoltenSlag);
  }

  /// <summary>
  /// Totals the charge in the shaft and sums its composition in the same walk. The composition is
  /// volume-weighted, so a shaft loaded with several grades burns at the column's true average coke
  /// ratio rather than at whatever the top pile happens to be.
  /// </summary>
  private int GetBlastMixCount(
    List<(BlockPos pos, BlockEntityCoalPile pile)> piles,
    out bool isFull,
    out BurdenMix mix
  )
  {
    // Composition assumed for legacy count-only blast mix. Its fuel share must match
    // BfReferenceFuelFrac (unstamped charge burns as standard grade) and its flux share must clear
    // every grade's flux floor, or an old world's charge would grade as a flux shortfall.
    float legacyFuel = IwexValues.BfDefaultFuelFrac;
    float legacyFlux = IwexValues.BfDefaultFluxFrac;
    float legacyIron = Math.Max(0f, 1f - legacyFuel - legacyFlux);

    int totalMix = 0;
    float iron = 0f;
    float flux = 0f;
    float fuel = 0f;

    foreach (var (_, pileBe) in piles)
    {
      // While lit, the furnace manages and keeps its hearth piles burning.
      if (State != FurnaceState.Idle)
      {
        BlastmixPiles.SetManagedByFurnace(pileBe, true);
        if (!pileBe.IsBurning)
          pileBe.TryIgnite();
      }

      if (pileBe.inventory == null)
        continue;

      foreach (var slot in pileBe.inventory)
      {
        if (slot.Empty || !IsCharge(slot.Itemstack))
          continue;

        int size = slot.StackSize;
        totalMix += size;

        BurdenMix stackMix = Burden.Is(slot.Itemstack)
          ? Burden.Read(slot.Itemstack)
          : default;

        if (stackMix.HasContent)
        {
          iron += stackMix.IronFrac * size;
          flux += stackMix.FluxFrac * size;
          fuel += stackMix.FuelFrac * size;
        }
        else
        {
          iron += legacyIron * size;
          flux += legacyFlux * size;
          fuel += legacyFuel * size;
        }
      }
    }

    mix = new BurdenMix(iron, flux, fuel);
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
    BlockPos lowerTapPos = GlobalOf(MetalTapCell);
    if (
      Api.World.BlockAccessor.GetBlockEntity(lowerTapPos)
        is not BlockEntityMoltenMetalTap lowerTap
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
    BlockPos higherTapPos = GlobalOf(SlagTapCell);
    if (
      Api.World.BlockAccessor.GetBlockEntity(higherTapPos)
        is not BlockEntityMoltenMetalTap higherTap
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

  // The residue itself (freeze the pool onto the hearth floor, burn the burden out by height, never
  // slag) is the core's, shared with every other furnace. All the blast furnace supplies is what its
  // pool was made of - a cupola swaps these four members for cast iron and inherits the rest.

  protected override AssetLocation SolidProductBlock { get; } =
    new("iwex", "solidifiediron");

  protected override float DrainedMetalUnits => _moltenIron;

  protected override void StampSolidProduct(BlockPos pos, int units)
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(pos)
      is BlockEntitySolidifiedIron solid
    )
    {
      solid.MetalCount = units;
      solid.MarkDirty(true);
    }
  }

  protected override void ClearMoltenPools()
  {
    _moltenIron = 0;
    _moltenSlag = 0;
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
      Lang.Get(IwexLang.BfInfoMolteniron, _moltenIron, _maxMoltenIron)
    );
    sb.AppendLine(
      Lang.Get(IwexLang.BfInfoMoltenslag, _moltenSlag, _maxMoltenSlag)
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
          ? IwexLang.BfInfoPartiallylit
          : IwexLang.BfInfoReady
      )
    );
  }

  #endregion
}
