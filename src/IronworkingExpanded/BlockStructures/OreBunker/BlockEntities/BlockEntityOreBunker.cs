using System;
using System.Text;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreBunker.Blocks;
using IronworkingExpanded.Compat;
using IronworkingExpanded.Items;
using IronworkingExpanded.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.OreBunker.BlockEntities;

/// <summary>
/// The 3×1×6 ore/blast-mix bunker. Construction is handled by the
/// <c>RightClickConstructable</c> behavior, which suppresses the default mesh, so the bunker
/// renders through a permanent <c>idle</c> animation re-tessellated to the currently-built
/// elements (same approach as the bessemer converter and the boilers). Inventory storage is
/// provided by the <see cref="BlockEntityContainer"/> base; a mixer will deposit finished blast
/// mix into it, and its contents spill on break.
/// </summary>
[BlockEntityRegister]
public class BlockEntityOreBunker : BlockEntityContainer
{
  private const int BunkerSlots = 9;
  private readonly InventoryOreBunker _inventory;

  // Guards the burden-coalescing pass against re-entering itself when its own slot writes fire
  // OnItemSlotModified (and suppresses it during the deliberate deposit paths below).
  private bool _normalizing;

  private BEBehaviorAnimatable? _animatable;
  private ExRightClickConstructable? _rcc;
  private bool _animatorReady;

  // Visual heap of stored burden: a flat ore surface rising with how full the bunker is. Client only.
  private OreSurfaceRenderer? _oreRenderer;

  // The stored burden visibly fills the interior between these pixel heights (entire internal space).
  private const float OreSurfaceYMin = 5f / 16f;
  private const float OreSurfaceYMax = 14f / 16f;

  public override InventoryBase Inventory => _inventory;
  public override string InventoryClassName => "orebunker";

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _rcc?.IsComplete ?? false;

  public BlockEntityOreBunker()
  {
    _inventory = new InventoryOreBunker(BunkerSlots, null, null, null) { Bunker = this };
  }

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    _inventory.LateInitialize(
      InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z,
      api
    );

    _animatable = GetBehavior<BEBehaviorAnimatable>();
    _rcc = GetBehavior<ExRightClickConstructable>();

    if (api is ICoreClientAPI capi)
    {
      if (_animatable != null)
      {
        // Re-render whenever the construction stage adds/removes elements.
        if (_rcc != null)
          _rcc.OnShapeChanged += OnConstructShapeChanged;

        RebuildAnimator(_rcc?.shape?.SelectiveElements);
        ApplyPose();
      }
      InitOreRenderer(capi);
      UpdateOreLevel();
    }
  }

  private string AnimCacheKey => "orebunker-" + Block.Variant["side"];

  public override void OnBlockRemoved()
  {
    if (_rcc != null)
      _rcc.OnShapeChanged -= OnConstructShapeChanged;
    _oreRenderer?.Dispose();
    _oreRenderer = null;
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    if (_rcc != null)
      _rcc.OnShapeChanged -= OnConstructShapeChanged;
    _oreRenderer?.Dispose();
    _oreRenderer = null;
    base.OnBlockUnloaded();
  }

  private void OnConstructShapeChanged(CompositeShape cs)
  {
    RebuildAnimator(cs?.SelectiveElements);
    ApplyPose();
  }

  /// <summary>
  /// (Re)builds the animator to render exactly the currently-built elements (only the mesh is
  /// filtered to <paramref name="selectiveElements"/>; the animator hierarchy stays the full shape).
  /// </summary>
  private void RebuildAnimator(string[]? selectiveElements)
  {
    if (Api is not ICoreClientAPI || _animatable == null)
      return;

    // CreateMesh resolves a FRESH shape each call; reusing one re-maps UVs into atlas space and
    // stretches textures. Rotation is applied by the renderer, not baked into the mesh.
    MeshData meshData = _animatable.animUtil.CreateMesh(
      AnimCacheKey,
      null,
      out Shape resolvedShape,
      null,
      new TesselationMetaData { SelectiveElements = selectiveElements }
    );

    _animatable.animUtil.InitializeAnimator(
      AnimCacheKey,
      meshData,
      resolvedShape,
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
    // A failed shape resolve leaves animUtil.animator null; only mark ready when it exists, so
    // ApplyPose never poses a null animator (vanilla GetBlockInfo would NRE). Same guard as the converter.
    _animatorReady = _animatable.animUtil.animator != null;
  }

  /// <summary>Holds the bunker visible via a permanent idle pose (RCC draws no mesh of its own).</summary>
  private void ApplyPose()
  {
    if (Api is not ICoreClientAPI || _animatable == null || !_animatorReady)
      return;

    var util = _animatable.animUtil;
    util.StartAnimation(
      new AnimationMetaData
      {
        Animation = "idle",
        Code = "idle",
        AnimationSpeed = 1f,
        EaseInSpeed = 3f,
        EaseOutSpeed = 3f,
      }.Init()
    );
  }

  #endregion

  #region Ore surface render

  /// <summary>Maximum burden (units) the bunker holds; the heap reads "full" at this amount.</summary>
  private static int MaxBurden => IwexValues.BunkerMaxBurden;

  /// <summary>Interior footprint of the stored heap, north-default frame (3 wide × 6 deep), inset 1px
  /// from the walls. Rotated by the structure angle to match the placed orientation.</summary>
  private static readonly Cuboidf[] OreSurfaceBoxes =
  [
    new Cuboidf(-15f, 0f, 1f, 31f, 16f, 95f),
  ];

  private void InitOreRenderer(ICoreClientAPI capi)
  {
    // Boxes are authored in the structure-offset frame, so they rotate by StructureAngle (the same
    // angle the fillers use), not Shape.rotateY - see the megablock surface-renderer rotation note.
    float rot =
      (float)(((Block as BlockOreBunker)?.StructureAngle ?? 0) * Math.PI / 180.0);
    _oreRenderer = new OreSurfaceRenderer(
      Pos,
      capi,
      OreSurfaceBoxes,
      rot,
      OreSurfaceYMin,
      OreSurfaceYMax,
      new AssetLocation("game:textures/block/coal/orecoalmix.png")
    );
    capi.Event.RegisterRenderer(_oreRenderer, EnumRenderStage.Opaque);
  }

  /// <summary>Raises the heap surface to match the stored amount (full at the configured capacity).</summary>
  private void UpdateOreLevel()
  {
    if (_oreRenderer != null)
      _oreRenderer.Fill = IsConstructed
        ? TotalContents / (float)MaxBurden
        : 0f;
  }

  #endregion

  #region Crate storage

  // The bunker behaves like a very large, GUI-less crate that holds ONE feedstock at a time: either a
  // single crushed-ore type, or burden of a single grade. A right-click with an accepted item in hand
  // deposits it, an empty-handed right-click withdraws a stack; the mixer drains burden in from above
  // and a chute can feed it too. Whatever the input path, it is gated by <see cref="CanAccept"/> so the
  // bunker never mixes ore with burden or two different ores. Burden of the same grade pools together:
  // its stored composition becomes the unit-weighted average of everything deposited (see
  // <see cref="NormalizeBurden"/>), so a part-coke and a standard load merge into one in-between mix.

  private static bool IsBurden(ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "burden" };

  // Any crushed-ore item: the vanilla "crushed-*" resource family (crushed iron, copper, ... and the
  // smex crushed coke), plus the modded crushed-iron variants the blast-furnace feed already knows.
  private static bool IsCrushedOre(ItemStack? stack)
  {
    string? path = stack?.Collectible?.Code?.Path;
    return path != null
      && (path.StartsWith("crushed-", StringComparison.Ordinal)
        || IronOreCompat.IsCrushedIronOre(path));
  }

  /// <summary>Total units stored across all slots (burden or ore, whichever the bunker currently holds).</summary>
  public int TotalContents
  {
    get
    {
      int total = 0;
      foreach (ItemSlot slot in _inventory)
        if (slot.Itemstack != null)
          total += slot.StackSize;
      return total;
    }
  }

  /// <summary>
  /// Whether <paramref name="stack"/> may enter the bunker right now. Consulted by every input path
  /// (player right-click, mixer drain, and - through <see cref="InventoryOreBunker.CanContain"/> - a
  /// chute). Rejects anything that isn't burden or crushed ore, anything once the bunker is full, and
  /// anything that doesn't align with the current contents: a held ore must match the stored ore's
  /// collectible, and held burden must share the stored burden's grade (so a different grade or the
  /// other category is declined). An empty bunker accepts any single burden or ore.
  /// </summary>
  public bool CanAccept(ItemStack? stack)
  {
    if (stack?.Collectible?.Code == null)
      return false;
    bool burden = IsBurden(stack);
    bool ore = !burden && IsCrushedOre(stack);
    if (!burden && !ore)
      return false;
    if (TotalContents >= MaxBurden)
      return false;

    string? incomingGrade = burden ? Burden.ProfileLangKey(Burden.Read(stack)) : null;
    foreach (ItemSlot slot in _inventory)
    {
      ItemStack? cur = slot.Itemstack;
      if (cur == null)
        continue;
      if (burden)
      {
        if (!IsBurden(cur) || Burden.ProfileLangKey(Burden.Read(cur)) != incomingGrade)
          return false;
      }
      else if (cur.Collectible != stack.Collectible)
        return false;
    }
    return true;
  }

  /// <summary>
  /// Moves an accepted stack from <paramref name="fromSlot"/> into the bunker. With
  /// <paramref name="wholeStack"/> the whole held stack is taken (ctrl+right-click); otherwise a single
  /// unit (plain right-click). Burden pools to a weighted-average grade; crushed ore stacks plainly.
  /// Server-side. Returns true when anything moved. Rejects misaligned/foreign stacks via
  /// <see cref="CanAccept"/>.
  /// </summary>
  public bool TryDeposit(ItemSlot fromSlot, bool wholeStack = true)
  {
    ItemStack? incoming = fromSlot.Itemstack;
    if (incoming?.Collectible == null || !CanAccept(incoming))
      return false;

    int maxStack = incoming.Collectible.MaxStackSize;
    int budget = wholeStack ? fromSlot.StackSize : Math.Min(1, fromSlot.StackSize);
    // Never accept more than the configured ceiling or the physical slot capacity, whichever is lower.
    budget = Math.Min(budget, Math.Min(MaxBurden, BunkerSlots * maxStack) - TotalContents);
    if (budget <= 0)
      return false;

    return IsBurden(incoming)
      ? DepositBurden(fromSlot, budget)
      : DepositOre(fromSlot, budget);
  }

  // Pulls up to `take` burden units out of fromSlot and re-pools the bunker to the new weighted-average
  // grade. The pooled mix is the unit-weighted average of everything stored plus the incoming slice.
  private bool DepositBurden(ItemSlot fromSlot, int take)
  {
    ItemStack incoming = fromSlot.Itemstack!;
    BurdenMix pooled = WeightedBurdenMix(out int existing); // already normalised fractions
    BurdenMix inMix = Burden.Read(incoming);
    int total = existing + take;
    // Average over normalised fractions, not the raw stamped parts: two batches may be stamped at
    // different scales while meaning the same proportions, so weight per-unit fractions by stack size.
    BurdenMix mix =
      new(
        (pooled.Iron * existing + inMix.IronFrac * take) / total,
        (pooled.Flux * existing + inMix.FluxFrac * take) / total,
        (pooled.Fuel * existing + inMix.FuelFrac * take) / total
      );

    fromSlot.TakeOut(take);
    fromSlot.MarkDirty();
    SetBurden(total, (Item)incoming.Collectible, mix);
    MarkDirty(true);
    return true;
  }

  // Stacks crushed ore the plain way: top up matching-collectible stacks, then spill into empty slots.
  private bool DepositOre(ItemSlot fromSlot, int budget)
  {
    ItemStack incoming = fromSlot.Itemstack!;
    int maxStack = incoming.Collectible.MaxStackSize;
    bool moved = false;

    _normalizing = true;
    try
    {
      foreach (ItemSlot slot in _inventory)
      {
        if (budget <= 0 || fromSlot.Empty)
          break;
        if (slot.Itemstack is not { } existing || existing.Collectible != incoming.Collectible)
          continue;
        int take = Math.Min(Math.Min(maxStack - slot.StackSize, fromSlot.StackSize), budget);
        if (take <= 0)
          continue;
        slot.Itemstack.StackSize += take;
        fromSlot.TakeOut(take);
        budget -= take;
        slot.MarkDirty();
        moved = true;
      }
      foreach (ItemSlot slot in _inventory)
      {
        if (budget <= 0 || fromSlot.Empty)
          break;
        if (!slot.Empty)
          continue;
        int take = Math.Min(fromSlot.StackSize, budget);
        slot.Itemstack = fromSlot.TakeOut(take);
        budget -= take;
        slot.MarkDirty();
        moved = true;
      }
    }
    finally
    {
      _normalizing = false;
    }

    if (moved)
    {
      fromSlot.MarkDirty();
      MarkDirty(true);
    }
    return moved;
  }

  // Unit-weighted average of every burden slot's composition, returned as normalised fractions (each
  // slot's per-unit proportions weighted by its stack size). totalUnits is the burden total; 0 = none.
  private BurdenMix WeightedBurdenMix(out int totalUnits)
  {
    float iron = 0f,
      flux = 0f,
      fuel = 0f;
    int total = 0;
    foreach (ItemSlot slot in _inventory)
    {
      if (!IsBurden(slot.Itemstack))
        continue;
      BurdenMix m = Burden.Read(slot.Itemstack);
      int n = slot.StackSize;
      iron += m.IronFrac * n;
      flux += m.FluxFrac * n;
      fuel += m.FuelFrac * n;
      total += n;
    }
    totalUnits = total;
    return total > 0 ? new BurdenMix(iron / total, flux / total, fuel / total) : default;
  }

  // Repacks the whole bunker into `total` units of `item`, every stack stamped with `mix`. Used for
  // burden only (the bunker holds nothing else when burden is present), so it clears all slots first.
  private void SetBurden(int total, Item item, BurdenMix mix)
  {
    int maxStack = item.MaxStackSize;
    int remaining = total;
    _normalizing = true;
    try
    {
      foreach (ItemSlot slot in _inventory)
      {
        if (remaining > 0)
        {
          int n = Math.Min(maxStack, remaining);
          ItemStack st = new(item, n);
          Burden.Write(st, mix);
          slot.Itemstack = st;
          remaining -= n;
        }
        else
          slot.Itemstack = null;
        slot.MarkDirty();
      }
    }
    finally
    {
      _normalizing = false;
    }
  }

  // Collapses any burden the bunker holds back into one uniform weighted-average grade. The deliberate
  // deposit paths already pool as they go; this catches burden that a chute dropped straight into a
  // slot (vanilla stacking won't merge two different compositions, so it lands un-pooled until here).
  private void NormalizeBurden()
  {
    BurdenMix mix = WeightedBurdenMix(out int total);
    if (total <= 0)
      return;
    Item? item = null;
    foreach (ItemSlot slot in _inventory)
      if (IsBurden(slot.Itemstack))
      {
        item = (Item)slot.Itemstack!.Collectible;
        break;
      }
    if (item != null)
      SetBurden(total, item, mix);
  }

  /// <summary>Hook for <see cref="InventoryOreBunker.OnItemSlotModified"/>: after a chute (or any other
  /// inventory-level) change, re-pool burden and re-height the visible heap. Server-authoritative and
  /// re-entry-guarded so the coalescing pass's own writes don't recurse.</summary>
  internal void OnInventoryModified()
  {
    if (_normalizing || Api?.Side != EnumAppSide.Server)
      return;
    NormalizeBurden();
    UpdateOreLevel();
  }

  /// <summary>Takes the whole most-recently-filled stack out of the bunker (LIFO), or null when empty.</summary>
  public ItemStack? TryWithdraw()
  {
    for (int i = _inventory.Count - 1; i >= 0; i--)
    {
      ItemSlot slot = _inventory[i];
      if (slot.Empty)
        continue;
      ItemStack taken = slot.TakeOutWhole();
      slot.MarkDirty();
      MarkDirty(true);
      return taken;
    }
    return null;
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    // A client receiving a content change re-heights the visible heap (renderer may not exist yet on load).
    UpdateOreLevel();
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
  {
    base.GetBlockInfo(forPlayer, sb);
    if (!IsConstructed)
      return;
    int total = TotalContents;
    if (total <= 0)
    {
      sb.AppendLine(Lang.Get("iwex:bunker-empty"));
      return;
    }
    ItemStack? sample = _inventory.FirstNonEmptySlot?.Itemstack;
    sb.AppendLine(Lang.Get("iwex:bunker-stored", total, sample?.GetName() ?? ""));
  }

  #endregion
}

/// <summary>
/// The bunker's inventory: a plain <see cref="InventoryGeneric"/> that gates every put against the
/// owning bunker's <see cref="BlockEntityOreBunker.CanAccept"/> (so chutes and auto-transfer obey the
/// one-feedstock rule), and re-pools burden after any inventory-level change.
/// </summary>
public class InventoryOreBunker(
  int quantitySlots,
  string? className,
  string? instanceID,
  ICoreAPI? api
) : InventoryGeneric(quantitySlots, className, instanceID, api)
{
  /// <summary>The owning bunker, wired up after construction; null only mid-construction.</summary>
  internal BlockEntityOreBunker? Bunker;

  public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot) =>
    base.CanContain(sinkSlot, sourceSlot) && (Bunker?.CanAccept(sourceSlot?.Itemstack) ?? true);

  public override void OnItemSlotModified(ItemSlot slot)
  {
    base.OnItemSlotModified(slot);
    Bunker?.OnInventoryModified();
  }
}
