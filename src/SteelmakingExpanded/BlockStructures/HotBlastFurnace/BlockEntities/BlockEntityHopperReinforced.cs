using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// Reinforced hopper: a small charge tank above the bell hopper, holding burden the burdenmaker has
/// already stamped. A right-click with charge fills the tank, an empty-handed right-click empties it, and
/// Ctrl + right-click toggles the bell hopper's dropping below. The tank is a single
/// <see cref="ItemStack"/>, so it holds one grade at a time and a mismatched deposit is refused. It is a
/// much smaller buffer than the tall hopper's tank, sized for skip-hoist feeding. The bell below pulls
/// from it (<see cref="DrawBurden"/>) into its own magazine and drips that down the shaft.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperReinforced : BlockEntity {
  // The whole tank is one charge stack (item identity = family/fuel, attributes = grade). Null when empty.
  private ItemStack? _tank;

  // The furnace this hopper ultimately charges, resolved by the bounded multiblock scan every furnace part
  // uses (the bell below keeps an identical link). The reinforced hopper stands eight cells above the core
  // on the shipped drawing, which is what ComponentScanBelow covers - see BlockEntityFurnaceCore. The link
  // is what keeps the tank from holding an opinion of its own about what is chargeable; see IsChargeItem.
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  // Cached, untranslated mesh of the burden contents pile (built lazily client-side).

  // The contents pile is drawn between these heights (in 1/16 block units) inside the hopper, scaling
  // with how full the tank is.
  private const float ContentsMinY = 9f;
  private const float ContentsMaxY = 14f;

  /// <summary>Burden units currently held (0 when empty). Serialized, so the client HUD/mesh read it.</summary>
  public int TankCount => _tank?.StackSize ?? 0;

  /// <summary>The burden stack the tank holds, or null when empty, for the block's break drops. The
  /// caller must not mutate it - clone first.</summary>
  public ItemStack? TankContents => _tank;

  /// <summary>Maximum units the tank holds. Live config value; a small buffer, sized for skip-hoist
  /// feeding.</summary>
  public int Capacity => SmexValues.HopperReinforcedCapacity;

  /// <summary>Whether the tank is at capacity.</summary>
  public bool IsFull => TankCount >= Capacity;

  #region Deposit / withdraw (driven from the block)

  /// <summary>
  /// Whether <paramref name="stack"/> is charge the furnace under this hopper takes. Answered by the
  /// anchored core, not here: one tank block serves several machines that each declare a different charge
  /// and burn their own fuel. Must not be narrowed to burden only - this is the only charging cell the hot
  /// furnace's drawing carries, so a burden-only gate would refuse coke and charcoal and leave the shaft
  /// unable to satisfy <c>RacewayIsLightable</c>. False when the hopper stands over no furnace.
  /// </summary>
  public bool IsChargeItem(ItemStack? stack) =>
    Anchor.Resolve() is { } core && core.IsChargeItem(stack);

  /// <summary>
  /// Whether <paramref name="stack"/> can enter the tank now: it must be charge this furnace takes
  /// (<see cref="IsChargeItem"/>) and, once the tank holds something, match it in item and grade, since one
  /// stack cannot carry two grades. An empty tank accepts any single charge. The single-stack rule is what
  /// makes one load lay one band type; a tank holding coke and burden at once would drip them interleaved.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    IsChargeItem(stack) && (_tank == null || IsMergeable(stack));

  /// <summary>
  /// Moves burden from <paramref name="fromSlot"/> into the tank. With <paramref name="wholeStack"/> it
  /// takes the whole held stack (ctrl+right-click), otherwise a single unit (plain right-click), each
  /// capped by the remaining room. Server-side. Returns true when anything moved; false for a foreign or
  /// mismatched stack, or a full tank - the block turns the mismatch into an in-game error.
  /// </summary>
  public bool TryDeposit(ItemSlot fromSlot, bool wholeStack) {
    ItemStack? incoming = fromSlot.Itemstack;
    if (!Accepts(incoming))
      return false;

    int room = Capacity - TankCount;
    int take = Math.Min(wholeStack ? fromSlot.StackSize : 1, room);
    if (take <= 0)
      return false;

    if (_tank == null) {
      _tank = incoming!.Clone();
      _tank.StackSize = take;
    } else {
      _tank.StackSize += take;
    }

    fromSlot.TakeOut(take);
    fromSlot.MarkDirty();
    MarkDirty(true);
    return true;
  }

  /// <summary>Empties the tank, handing back the whole burden stack (or null when already empty).</summary>
  public ItemStack? TryWithdraw() {
    if (_tank == null || _tank.StackSize <= 0)
      return null;
    ItemStack taken = _tank;
    _tank = null;
    MarkDirty(true);
    return taken;
  }

  /// <summary>The tank's burden stack for a read-only peek (the bell reads its grade before pulling). Do
  /// not mutate - clone first.</summary>
  public ItemStack? PeekTank() => _tank;

  /// <summary>Removes up to <paramref name="max"/> burden units from the tank and returns them as a stack
  /// (its grade preserved), or null when empty. The bell hopper below draws its magazine this way.</summary>
  public ItemStack? DrawBurden(int max) {
    if (_tank == null || _tank.StackSize <= 0 || max <= 0)
      return null;

    int take = Math.Min(max, _tank.StackSize);
    ItemStack drawn = _tank.Clone();
    drawn.StackSize = take;

    _tank.StackSize -= take;
    if (_tank.StackSize <= 0)
      _tank = null;

    MarkDirty(true);
    return drawn;
  }

  /// <summary>Flips the bell hopper below between dropping and stopped (the Ctrl + right-click gesture).
  /// Server-side.</summary>
  public void ToggleBellDropping() {
    if (
      Api.Side == EnumAppSide.Server
      && Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy())
        is BlockEntityHopperBell bell
    ) {
      bell.IsDropping = !bell.IsDropping;
      bell.MarkDirty(true);
    }
  }

  // Same item and same stamped grade: one stack cannot hold two grades, so a different mix (or a different
  // item) is refused rather than pooled. Fuels are separated by the item clause alone - coke and charcoal
  // are distinct collectibles, and Burden.Read of an unstamped fuel stack is `default` on both sides. The
  // separation matters because a band stores one material code and the two fuels differ in CarbonPerUnit.
  private bool IsMergeable(ItemStack? stack) =>
    _tank != null
    && stack?.Collectible == _tank.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_tank));

  #endregion

  #region Rendering

  /// <summary>
  /// Draws the burden contents pile on top of the normal hopper mesh, raised between
  /// <see cref="ContentsMinY"/> and <see cref="ContentsMaxY"/> in proportion to how full the tank is.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (
      TankCount > 0
      && Api is ICoreClientAPI capi
      && ExMeshCache.GetOrCreate(
        capi,
        Block,
        "contents",
        () => BuildContentsMesh(tesselator)
      )
        is { } contents
    ) {
      float fill = GameMath.Clamp((float)TankCount / Capacity, 0f, 1f);
      float yOffset =
        (ContentsMinY + fill * (ContentsMaxY - ContentsMinY)) / 16f;

      // Clone first: the cached mesh is shared by every hopper of this blocktype and Translate moves it
      // in place, so lifting the cached one would raise the pile in every other hopper too, cumulatively.
      MeshData mesh = contents.Clone();
      mesh.Translate(0f, yOffset, 0f);
      mesher.AddMeshData(mesh);
    }

    // Keep the default hopper block mesh as well.
    return base.OnTesselation(mesher, tesselator);
  }

  private MeshData? BuildContentsMesh(ITesselatorAPI tesselator) {
    if (
      ExMeshCache.LoadShape(
        Api,
        new AssetLocation("iiex:shapes/ore/burden.json")
      )
      is not { } shape
    )
      return null;

    tesselator.TesselateShape(Block, shape, out MeshData mesh);
    return mesh;
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _tank = tree.GetItemstack("tank");
    _tank?.ResolveBlockOrItem(worldForResolving);
    // A stack whose item no longer resolves, or a zero stack, reads as empty.
    if (_tank?.Collectible == null || _tank.StackSize <= 0)
      _tank = null;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_tank != null)
      tree.SetItemstack("tank", _tank);
  }

  /// <summary>Maps the tank's charge stack, so a reinforced hopper pasted into another world resolves
  /// its contents against that world's item ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) =>
    _tank?.Collectible.OnStoreCollectibleMappings(
      Api.World,
      new DummySlot(_tank),
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
    // source world's value, which would resolve to whatever owns that id there. Null the stack instead
    // of keeping a mis-resolved one, matching vanilla's BEIngotMold.cs:806-809.
    if (
      _tank?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve)
      == false
    )
      _tank = null;
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (_tank == null || _tank.StackSize <= 0)
      dsc.AppendLine(Lang.Get("smex:hopper-empty"));
    else
      dsc.AppendLine(
        Lang.Get(
          "smex:hopper-holds",
          _tank.StackSize,
          Capacity,
          _tank.GetName()
        )
      );

    if (
      Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy())
      is BlockEntityHopperBell bell
    ) {
      dsc.AppendLine(
        Lang.Get(
          "smex:hopper-info-bell",
          bell.IsDropping
            ? Lang.Get("smex:hopper-state-dropping")
            : Lang.Get("smex:hopper-state-stopped")
        )
      );
      dsc.AppendLine(
        Lang.Get(
          "smex:hopper-info-magazine",
          bell.BlastMixMagazine,
          bell.MaxMagazineCapacity
        )
      );
      if (bell.IsFurnaceFull())
        dsc.AppendLine(Lang.Get("smex:hopper-info-furnacefull"));
    } else {
      dsc.AppendLine(Lang.Get("smex:hopper-info-nobell"));
    }
  }

  #endregion
}
