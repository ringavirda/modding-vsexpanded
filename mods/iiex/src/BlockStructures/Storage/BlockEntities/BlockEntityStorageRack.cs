using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Storage.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Storage.BlockEntities;

/// <summary>
/// What is on a rack: an ordered list of runs, each a stack and the cells it fills. Capacity is length -
/// a stack occupies as many cells as <c>config/bayoccupancy/</c> declares for it, and the rack is full
/// when the runs fill the row. Nothing stacks upward and nothing sits across the row.
/// </summary>
[BlockEntityRegister]
public class BlockEntityStorageRack : ExBlockEntity, ITexPositionSource {
  /// <summary>One stored stack and the cells it covers.</summary>
  /// <param name="Run">Where it lies in the row.</param>
  /// <param name="Stack">What lies there. Whole stacks are laid and taken: a piece carries its own state
  /// and must not be split or merged.</param>
  public readonly record struct Load(BayRun Run, ItemStack Stack);

  private readonly List<Load> _loads = [];

  /// <summary>What the rack holds, in the order it was laid.</summary>
  public IReadOnlyList<Load> Loads => _loads;

  private BlockStorageRack? Rack => Block as BlockStorageRack;

  /// <summary>Cells this rack's footprint gives it, or 0 before its block is known.</summary>
  public int Cells => Rack?.Cells ?? 0;

  /// <summary>Cells no load covers.</summary>
  public int FreeCells => BayLayout.FreeCells(Runs(), Cells);

  private List<BayRun> Runs() {
    var runs = new List<BayRun>(_loads.Count);
    foreach (Load load in _loads)
      runs.Add(load.Run);
    return runs;
  }

  #region Laying and taking

  /// <summary>
  /// How many cells a stack of <paramref name="stack"/> takes on this rack, or null when the rack's
  /// catalogue does not list it - which is the rack refusing it. A rack is not a chest: it holds what it
  /// is told it can hold, at the length it is told, because anything it holds it also has to draw.
  /// </summary>
  public int? CellsFor(ItemStack? stack) =>
    BayOccupancyRegistry.Shared.CellsFor(
      BlockStorageRack.StoreKey,
      stack?.Collectible?.Code?.ToString()
    );

  /// <summary>
  /// Lays the whole of <paramref name="from"/> on the lowest run that fits it. Refused when the item is
  /// not in the catalogue or no gap is wide enough.
  /// </summary>
  /// <param name="commit">False on the client, which predicts nothing: the server owns the contents and
  /// the client redraws when the tree arrives.</param>
  public bool TryLay(ItemSlot from, bool commit = true) {
    if (from.Itemstack is not { } stack)
      return false;
    if (CellsFor(stack) is not { } length)
      return false;
    if (BayLayout.Fit(Runs(), Cells, length) is not { } start)
      return false;
    if (!commit)
      return true;

    _loads.Add(new Load(new BayRun(start, length), stack.Clone()));
    from.Itemstack = null;
    from.MarkDirty();
    Changed();
    return true;
  }

  /// <summary>
  /// Takes back whatever covers <paramref name="cell"/>, or null when that cell is empty. Any cell of a
  /// run takes that run, which is what makes every cell of the rack work the whole of it.
  /// </summary>
  public ItemStack? TryTake(int cell, bool commit = true) {
    if (BayLayout.IndexAt(Runs(), cell) is not { } index)
      return null;

    ItemStack stack = _loads[index].Stack;
    if (!commit)
      return stack;

    _loads.RemoveAt(index);
    Changed();
    return stack;
  }

  /// <summary>Everything on the rack, taken off it. What breaking the rack spawns.</summary>
  public IReadOnlyList<ItemStack> TakeAll() {
    var stacks = new List<ItemStack>(_loads.Count);
    foreach (Load load in _loads)
      stacks.Add(load.Stack);
    _loads.Clear();
    Changed();
    return stacks;
  }

  private void Changed() {
    MarkDirty(true);
    RebuildMeshes();
  }

  #endregion

  #region Render

  // The contents are composed from each stored item's own art rather than from anything drawn on the
  // rack, which is what lets it hold forms nobody has drawn a rack variant for. The frame is the block's
  // own mesh and is left to the engine - hence `base.OnTesselation` rather than `return true`.

  private ICoreClientAPI? _capi;
  private CollectibleObject? _tesselating;
  private Shape? _tesselatingShape;
  private MeshData?[] _meshes = [];
  private float[][] _transforms = [];

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _capi = api as ICoreClientAPI;
    RebuildMeshes();
  }

  /// <summary>
  /// Builds one mesh per load. Runs on the main thread - from <c>Initialize</c> and from every content
  /// change - and never from <see cref="OnTesselation"/>, because resolving an item's texture may insert
  /// it into the block atlas and the tesselation pass runs on a chunk worker.
  /// </summary>
  private void RebuildMeshes() {
    if (_capi == null)
      return;

    _meshes = new MeshData?[_loads.Count];
    _transforms = new float[_loads.Count][];
    for (int i = 0; i < _loads.Count; i++) {
      _meshes[i] = ItemMesh(_loads[i].Stack);
      _transforms[i] = TransformFor(_loads[i].Run);
    }
    _capi.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>
  /// One stored stack drawn with block-atlas coordinates. A mesh handed to the terrain pool must carry
  /// those: tesselating through the item atlas gives UVs that address the wrong sheet and the piece
  /// renders as somebody else's texture.
  /// </summary>
  private MeshData? ItemMesh(ItemStack stack) {
    if (_capi == null)
      return null;

    if (stack.Class == EnumItemClass.Block)
      return _capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();

    _tesselating = stack.Collectible;
    _tesselatingShape =
      stack.Item?.Shape?.Base == null
        ? null
        : _capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
    _capi.Tesselator.TesselateItem(stack.Item, out MeshData mesh, this);
    return mesh;
  }

  /// <summary>
  /// Where a run sits, in the rack's own frame, turned to the rack's facing. The piece is centred on the
  /// cells it covers, so a three-cell slab lies down the middle of the rack rather than at one end, and
  /// it rides at the height of the drawn rails.
  /// </summary>
  private float[] TransformFor(BayRun run) =>
    new Matrixf()
      .Translate(0.5f, 0f, 0.5f)
      .RotateYDeg(Block?.Shape?.rotateY ?? 0f)
      .Translate(0f, RailHeight, -CentreOf(run))
      .Translate(-0.5f, 0f, -0.5f)
      .Values;

  /// <summary>Height the drawn rails carry a piece at, in blocks - the shelf runs at y 4..8 of the
  /// art.</summary>
  private const float RailHeight = 8f / 16f;

  /// <summary>
  /// How far down the row a run's middle lies, in cells from the principal. The row runs away from the
  /// principal, so the caller negates it; a one-cell run at cell 0 sits at 0 and a three-cell run at 1.
  /// </summary>
  public static float CentreOf(BayRun run) => run.Start + (run.Length - 1) / 2f;

  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    for (int i = 0; i < _meshes.Length && i < _transforms.Length; i++)
      if (_meshes[i] is { } mesh)
        mesher.AddMeshData(mesh, _transforms[i]);

    // False, through base: the rack's own frame is the default block mesh and still has to be drawn.
    return base.OnTesselation(mesher, tesselator);
  }

  #region ITexPositionSource

  // Vanilla's display-container resolution order, so a stored item's textures are found the same way a
  // shelf or a display case finds them.

  /// <inheritdoc/>
  public Size2i AtlasSize => _capi!.BlockTextureAtlas.Size;

  /// <inheritdoc/>
  public TextureAtlasPosition this[string textureCode] {
    get {
      IDictionary<string, CompositeTexture>? textures = _tesselating
        is Item item
        ? item.Textures
        : (_tesselating as Block)?.Textures;

      AssetLocation? path = null;
      if (textures?.TryGetValue(textureCode, out CompositeTexture? tex) == true)
        path = tex.Baked.BakedName;
      if (path == null && textures?.TryGetValue("all", out tex) == true)
        path = tex.Baked.BakedName;
      _tesselatingShape?.Textures.TryGetValue(textureCode, out path);
      path ??= new AssetLocation(textureCode);

      return TexPos(path);
    }
  }

  private TextureAtlasPosition TexPos(AssetLocation path) {
    TextureAtlasPosition? pos = _capi!.BlockTextureAtlas[path];
    if (pos != null)
      return pos;

    // Inserting is main-thread work, which is why the meshes are built off the tesselation pass.
    if (!_capi.BlockTextureAtlas.GetOrInsertTexture(path, out _, out pos, null)) {
      _capi.World.Logger.Warning(
        "[iiex] storage rack: {0} names texture {1}, which does not exist",
        _tesselating?.Code,
        path
      );
      return _capi.BlockTextureAtlas.UnknownTexturePosition;
    }
    return pos;
  }

  #endregion

  #endregion

  #region Serialization

  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      "loads",
      tree => {
        tree.SetInt("loads", _loads.Count);
        for (int i = 0; i < _loads.Count; i++) {
          tree.SetInt($"load{i}Start", _loads[i].Run.Start);
          tree.SetInt($"load{i}Length", _loads[i].Run.Length);
          tree.SetItemstack($"load{i}Stack", _loads[i].Stack);
        }
      },
      (tree, worldForResolving) => {
        _loads.Clear();
        int count = tree.GetInt("loads");
        for (int i = 0; i < count; i++) {
          ItemStack? stack = tree.GetItemstack($"load{i}Stack");
          // Resolved, or the stack has no Collectible and every read of it - the catalogue lookup, the
          // readout, the mesh - silently answers nothing while the rack looks loaded.
          stack?.ResolveBlockOrItem(worldForResolving);
          if (stack?.Collectible == null)
            continue;

          int length = tree.GetInt($"load{i}Length", 1);
          _loads.Add(
            new Load(new BayRun(tree.GetInt($"load{i}Start"), length), stack)
          );
        }
      }
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    RebuildMeshes();
  }

  /// <summary>Maps every load's stack, so a loaded rack pasted into another world resolves each piece
  /// against that world's ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    foreach (Load load in _loads)
      load.Stack.Collectible?.OnStoreCollectibleMappings(
        Api.World,
        new DummySlot(load.Stack),
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
    // A false return means the destination world has no such item; FixMapping leaves Id at the source
    // world's value, which would resolve to whatever owns that id there. Drop the load rather than keep a
    // mis-resolved one - and drop the whole run with it, so the cells it held come back free instead of
    // being reserved by a piece that is not there.
    _loads.RemoveAll(load =>
      load.Stack.FixMapping(
        oldBlockIdMapping,
        oldItemIdMapping,
        worldForResolve
      ) == false
    );
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (_loads.Count == 0) {
      dsc.AppendLine(Lang.Get("iiex:rack-empty", Cells));
      return;
    }

    foreach (Load load in _loads)
      dsc.AppendLine(
        Lang.Get(
          "iiex:rack-holds",
          load.Stack.StackSize,
          load.Stack.GetName(),
          load.Run.Length
        )
      );
    dsc.AppendLine(Lang.Get("iiex:rack-free", FreeCells, Cells));
  }

  #endregion
}
