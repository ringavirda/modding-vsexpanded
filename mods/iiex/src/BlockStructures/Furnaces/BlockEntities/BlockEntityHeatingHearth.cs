using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Forming;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The reheat furnace's hearth: three rows of stock in the flame's path, one piece per row. The bed is two
/// cells deep so that a slab fits. A lit furnace soaks every piece lying here at its own pace
/// (<see cref="SoakTick"/>); an unlit one holds them and lets the engine's own cooling have them.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHeatingHearth : BlockEntityFurnacePart {
  // One slot per row. Whole stacks are never taken: each piece carries its own state (per-side thickness,
  // temperature) and must not merge, which is why the stock items are maxstacksize 1.
  private readonly ItemStack?[] _rows = new ItemStack?[
    HeatingHearthLayout.Rows
  ];

  /// <summary>The piece lying in <paramref name="row"/>, if any.</summary>
  public ItemStack? StockIn(HearthRows.Row row) => _rows[(int)row];

  /// <summary>How many rows carry a piece.</summary>
  public int LoadedRows {
    get {
      int n = 0;
      foreach (ItemStack? s in _rows)
        if (s != null)
          n++;
      return n;
    }
  }

  /// <summary>How many of the loaded rows are at or above rolling heat, and so ready to carry to the
  /// mill.</summary>
  public int RowsAtRollingHeat {
    get {
      if (Api is not { } api)
        return 0;
      int n = 0;
      foreach (ItemStack? s in _rows)
        if (
          s?.Collectible?.GetTemperature(api.World, s)
          >= IiexValues.RollingTempC
        )
          n++;
      return n;
    }
  }

  private bool CentreLoaded => _rows[(int)HearthRows.Row.Centre] != null;

  #region Loading

  /// <summary>
  /// Lays one piece in <paramref name="row"/>, taken from <paramref name="from"/>. Refused when the row is
  /// occupied, unreachable past a loaded centre, or the item is not stock the hearth has a bed for.
  /// </summary>
  public bool TryLoad(HearthRows.Row row, ItemSlot from) {
    if (!HearthRows.CanReach(row, CentreLoaded))
      return false;
    if (_rows[(int)row] != null || from.Empty)
      return false;
    if (
      HeatingHearthLayout.StockOf(from.Itemstack?.Collectible?.Code?.Path)
      == null
    )
      return false;

    _rows[(int)row] = from.TakeOut(1);
    Changed();
    return true;
  }

  /// <summary>Takes the piece out of <paramref name="row"/>, or null when there is none or it is out of
  /// reach.</summary>
  public ItemStack? TryTake(HearthRows.Row row) {
    if (_rows[(int)row] is not { } stack)
      return null;
    // A loaded centre blocks the flanks on the way out as well as in, so a full hearth unloads
    // centre-first.
    if (row != HearthRows.Row.Centre && CentreLoaded)
      return null;
    _rows[(int)row] = null;
    Changed();
    return stack;
  }

  private void Changed() {
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>
  /// Drops every piece on the bed. Each is unique - its own gauge, its own crop tally, its own heat - so
  /// breaking a loaded hearth without this destroys work that cannot be made again by repeating a recipe.
  /// </summary>
  public override void OnBlockBroken(IPlayer? byPlayer = null) {
    if (Api?.Side == EnumAppSide.Server)
      for (int i = 0; i < _rows.Length; i++)
        if (_rows[i] is { } stack) {
          _rows[i] = null;
          Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
        }
    base.OnBlockBroken(byPlayer);
  }

  #endregion

  #region Soak

  /// <summary>
  /// Puts <paramref name="dt"/> seconds of a chamber at <paramref name="furnaceC"/> into every piece on
  /// the bed, each at its own pace: heat crosses a piece at its surface, so a rod comes up in a fraction
  /// of the time a slab takes and thickness alone decides which. Nothing here is per-row - the bed soaks
  /// three pieces at once for the same fire, which is the whole of the furnace's advantage over a forge.
  /// </summary>
  /// <remarks>
  /// Driven from the furnace rather than from a listener of this hearth's own, so it rides the core's
  /// bounded away-catch-up. A hearth ticking itself would look identical while the chunk was loaded and
  /// teleport the soak the moment it was not.
  /// </remarks>
  /// <returns>Whether any piece moved, so a caller can mark the bed dirty once for the whole tick.</returns>
  public bool SoakTick(float furnaceC, float dt) {
    if (Api is not { Side: EnumAppSide.Server } api || dt <= 0f)
      return false;

    bool moved = false;
    foreach (ItemStack? stack in _rows) {
      if (stack?.Collectible is not { } collectible)
        continue;
      // The section is what paces the soak, and it lives on the work piece. A stack that resolves to none
      // is not stock the mill would take either, so there is nothing here to bring to rolling heat.
      if (WorkPiece.FromStack(stack) is not { } piece)
        continue;

      // A chamber hotter than the stock's own melting point would otherwise cook it on the bed. The
      // approach is asymptotic, so clamping the target at the melting point is enough to keep a piece
      // under it however long it soaks - no margin needed.
      float melting = collectible.GetMeltingPoint(
        api.World,
        null,
        new DummySlot(stack)
      );
      float target = melting > 0f ? Math.Min(furnaceC, melting) : furnaceC;

      float was = collectible.GetTemperature(api.World, stack);
      float now = RollingPass.Soak(
        was,
        target,
        IiexValues.ReheatRateK
          * RollingPass.AreaOverVolume(piece.Width, piece.Thickness),
        dt
      );
      if (now <= was)
        continue;

      collectible.SetTemperature(api.World, stack, now, delayCooldown: false);
      moved = true;
    }

    if (moved)
      MarkDirty();
    return moved;
  }

  #endregion

  #region Render

  // The shape has no animations, so contents are drawn through a per-BE SelectiveElements set rather than
  // an animator.
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Api is not ICoreClientAPI capi)
      return base.OnTesselation(mesher, tesselator);

    var forms = new HeatingHearthLayout.Stock?[HeatingHearthLayout.Rows];
    for (int i = 0; i < forms.Length; i++)
      forms[i] = HeatingHearthLayout.StockOf(_rows[i]?.Collectible?.Code?.Path);

    // Keyed on what is lying on the bed, the way vanilla's firepit keys on burn and content state. The
    // key space is bounded by 6^3 per orientation and fills only with states actually built.
    if (
      ExMeshCache.GetOrCreate(
        capi,
        Block,
        string.Join('|', forms),
        () => BuildBedMesh(tesselator, forms)
      ) is { } mesh
    )
      mesher.AddMeshData(mesh);

    // True: this block entity draws its whole mesh, so the default block mesh must not also be drawn. It
    // would show every stock form in every row at once.
    return true;
  }

  private MeshData? BuildBedMesh(
    ITesselatorAPI tesselator,
    HeatingHearthLayout.Stock?[] forms
  ) {
    if (
      ExMeshCache.LoadShape(Api, ExMeshCache.ShapePathOf(Block))
      is not { } shape
    )
      return null;

    // Prune the element tree directly: the engine's selectiveElements matching is per-segment prefix
    // based and keeps or drops the wrong subtree for these element names.
    tesselator.TesselateShape(
      Block,
      ExShapeElements.Pruned(shape, HeatingHearthLayout.ElementsFor(forms)),
      out MeshData mesh
    );
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }

  #endregion

  #region Serialization

  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      "rows",
      tree => {
        for (int i = 0; i < HeatingHearthLayout.Rows; i++)
          if (_rows[i] is { } stack)
            tree.SetItemstack("row" + i, stack);
          else
            tree.RemoveAttribute("row" + i);
      },
      (tree, worldForResolving) => {
        for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
          _rows[i] = tree.GetItemstack("row" + i);
          // Stacks read off a tree carry no resolved collectible; without this the render path reads a
          // null Code and the piece does not draw.
          _rows[i]?.ResolveBlockOrItem(worldForResolving);
        }
      }
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>Maps every row's piece, so a hearth mid-reheat pasted into another world still resolves
  /// each stock item against that world's ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    foreach (ItemStack? stack in _rows)
      stack?.Collectible?.OnStoreCollectibleMappings(
        Api.World,
        new DummySlot(stack),
        blockIdMapping,
        itemIdMapping
      );
  }

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    base.OnLoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping,
      schematicSeed,
      resolveImports
    );
    // A false return means the destination world has no such item/block; FixMapping leaves Id at the
    // source world's value, which would resolve to whatever owns that id there. Null the stack instead
    // of keeping a mis-resolved one, matching vanilla's BEIngotMold.cs:806-809. An index loop, not a
    // foreach, since the null-out has to land back in the array.
    for (int i = 0; i < _rows.Length; i++)
      if (
        _rows[i]
          ?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve)
        == false
      )
        _rows[i] = null;
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (Core is null) {
      dsc.AppendLine(Lang.Get(IiexLang.FurnacepartNofurnace));
      return;
    }
    dsc.AppendLine(
      Lang.Get(
        IiexLang.HeatinghearthLoaded,
        LoadedRows,
        HeatingHearthLayout.Rows
      )
    );
    if (CentreLoaded && LoadedRows < HeatingHearthLayout.Rows)
      dsc.AppendLine(Lang.Get(IiexLang.HearthCentreblocks));
  }

  #endregion
}
