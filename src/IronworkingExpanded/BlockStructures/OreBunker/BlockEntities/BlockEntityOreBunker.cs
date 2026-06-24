using ExpandedLib.Blocks.Construction;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
  private readonly InventoryGeneric _inventory;

  private BEBehaviorAnimatable? _animatable;
  private ExRightClickConstructable? _rcc;
  private bool _animatorReady;

  public override InventoryBase Inventory => _inventory;
  public override string InventoryClassName => "orebunker";

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _rcc?.IsComplete ?? false;

  public BlockEntityOreBunker()
  {
    _inventory = new InventoryGeneric(BunkerSlots, null, null);
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

    if (api is ICoreClientAPI && _animatable != null)
    {
      // Re-render whenever the construction stage adds/removes elements.
      if (_rcc != null)
        _rcc.OnShapeChanged += OnConstructShapeChanged;

      RebuildAnimator(_rcc?.shape?.SelectiveElements);
      ApplyPose();
    }
  }

  private string AnimCacheKey => "orebunker-" + Block.Variant["side"];

  public override void OnBlockRemoved()
  {
    if (_rcc != null)
      _rcc.OnShapeChanged -= OnConstructShapeChanged;
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    if (_rcc != null)
      _rcc.OnShapeChanged -= OnConstructShapeChanged;
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
}
