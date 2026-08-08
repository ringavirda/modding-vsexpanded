using ExpandedLib.Blocks.Structures;
using ExpandedLib.Renderers;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Shared base for the functional parts a furnace multiblock is built from - charge doors, hearths, the
/// chimney cap - none of which is the furnace itself. It supplies two things every part needs: the link to
/// the core whose layout owns the part's cell (<see cref="MultiblockAnchorLink{T}"/>, cached and
/// throttled), used to show furnace state, refuse work on an unfinished build and route the ctrl+shift
/// build outline; and a <see cref="ToggleAnimator"/>, since every part has an open/shut pose rather than a
/// running cycle. Building the animator stays with the subclass, which supplies cache key and rotation.
/// </summary>
public abstract class BlockEntityFurnacePart : BlockEntity, IMultiblockComponent {
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;
  private ToggleAnimator? _toggle;

  /// <summary>The furnace core this part belongs to, or null when it is standing on its own.</summary>
  protected BlockEntityFurnaceCore? Core => Anchor.Resolve();

  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <inheritdoc/>
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() =>
    Anchor.Resolve();

  /// <summary>Whether the furnace this part belongs to is built. Parts refuse work on an unfinished
  /// furnace: a hearth loaded before the walls are up holds a charge the player cannot retrieve.</summary>
  protected bool FurnaceComplete => Core?.StructureComplete == true;

  /// <summary>Animation cache key. Must be unique per part and per orientation, or the four rotations of
  /// one part share a joint hierarchy and only the first one built poses correctly.</summary>
  protected virtual string AnimCacheKey =>
    Block.Code.Path + "-" + (Block.Variant["side"] ?? "north");

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _toggle = new ToggleAnimator(this, BuildAnimator);
    _toggle.Initialize(ApplyPose);
  }

  /// <summary>
  /// Loads the block's own shape and initialises the animator on it, keyed by
  /// <see cref="AnimCacheKey"/>. Does nothing when the shape asset cannot be resolved.
  /// </summary>
  private void BuildAnimator(BEBehaviorAnimatable animatable) {
    var capi = (ICoreClientAPI)Api;
    Shape? shape = capi
      .Assets.TryGet(
        Block
          .Shape.Base.Clone()
          .WithPathPrefixOnce("shapes/")
          .WithPathAppendixOnce(".json")
      )
      ?.ToObject<Shape>();
    if (shape == null)
      return;

    animatable.animUtil.InitializeAnimator(
      AnimCacheKey,
      shape,
      capi.Tesselator.GetTextureSource(Block),
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
  }

  /// <summary>
  /// Selects the part's pose from its own state. Overrides must route through <see cref="Pose"/>. The
  /// default is a no-op for the hearths, whose shape is static and draws through <c>OnTesselation</c>.
  /// </summary>
  protected virtual void ApplyPose() { }

  /// <summary>Runs <paramref name="pose"/> only when a real animator exists (client, shape resolved).</summary>
  protected void Pose(System.Action<BlockEntityAnimationUtil> pose) =>
    _toggle?.Pose(pose);

  /// <summary>Re-poses after a wrench rotation, which rebuilds the block under a new orientation.</summary>
  public override void OnExchanged(Block block) {
    base.OnExchanged(block);
    _toggle?.Rebuild();
    ApplyPose();
  }

  #endregion

  /// <summary>
  /// Starts <paramref name="clip"/> and stops every other clip in <paramref name="group"/>, so at most one
  /// of a mutually exclusive set runs. Passing null for <paramref name="clip"/> stops them all. Poses ease
  /// in and out rather than snapping.
  /// </summary>
  protected void PoseOneOf(string? clip, params string[] group) =>
    Pose(util => {
      foreach (string other in group)
        if (other != clip)
          util.StopAnimation(other);
      if (clip != null)
        util.StartAnimation(
          new AnimationMetaData {
            Animation = clip,
            Code = clip,
            AnimationSpeed = 1.5f,
            EaseInSpeed = 6f,
            EaseOutSpeed = 6f,
          }.Init()
        );
    });
}
