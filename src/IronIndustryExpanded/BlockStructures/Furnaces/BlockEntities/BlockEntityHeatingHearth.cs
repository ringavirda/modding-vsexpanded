using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The reheat furnace's hearth: three rows of stock in the flame's path, one piece per row. The bed is two
/// cells deep so that a slab fits. Stock keeps cooling while it lies here; the reheat cycle that puts heat
/// back into the pieces is not implemented, so the hearth only holds and draws them.
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

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++)
      if (_rows[i] is { } stack)
        tree.SetItemstack("row" + i, stack);
      else
        tree.RemoveAttribute("row" + i);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
      _rows[i] = tree.GetItemstack("row" + i);
      // Stacks read off a tree carry no resolved collectible; without this the render path reads a null
      // Code and the piece does not draw.
      _rows[i]?.ResolveBlockOrItem(worldForResolving);
    }
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>Maps every row's piece, so a hearth mid-reheat pasted into another world still resolves
  /// each stock item against that world's ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
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
      dsc.AppendLine(Lang.Get("iiex:furnacepart-nofurnace"));
      return;
    }
    dsc.AppendLine(
      Lang.Get(
        "iiex:heatinghearth-loaded",
        LoadedRows,
        HeatingHearthLayout.Rows
      )
    );
    if (CentreLoaded && LoadedRows < HeatingHearthLayout.Rows)
      dsc.AppendLine(Lang.Get("iiex:hearth-centreblocks"));
  }

  #endregion
}
