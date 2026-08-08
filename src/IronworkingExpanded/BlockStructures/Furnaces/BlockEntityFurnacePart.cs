using ExpandedLib.Blocks.Structures;
using ExpandedLib.Renderers;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// Shared base for the <b>functional parts</b> a furnace multiblock is built out of - the charge doors,
/// the hearths, the chimney cap. Each is a block the player places during construction and then works
/// through; none of them is the furnace itself.
/// <para>
/// Two things every one of them needs, and nothing else did:
/// </para>
/// <list type="bullet">
/// <item><b>Knowing which furnace it belongs to.</b> A part scans for the core whose layout owns its cell
/// (<see cref="MultiblockAnchorLink{T}"/>, cached and throttled), which is what lets it show the furnace's
/// state, refuse to work on an unfinished build, and route the ctrl+shift build outline. The tap already
/// did this; the parts do it identically, so it lives here.</item>
/// <item><b>A toggled animation.</b> All of them have an open/shut or raised/lowered pose rather than a
/// running cycle, so they take the light <see cref="ToggleAnimator"/> path (not the RCC
/// <c>ConstructedAnimator</c>) and inherit its single null-animator ready-guard.</item>
/// </list>
/// <para>
/// The base deliberately does <b>not</b> own the animator <em>build</em>: cache keys and rotation differ
/// per part, and that variation is exactly what <see cref="ToggleAnimator"/> takes as a delegate.
/// </para>
/// </summary>
public abstract class BlockEntityFurnacePart : BlockEntity, IMultiblockComponent
{
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
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() => Anchor.Resolve();

  /// <summary>Whether the furnace this part belongs to is built and running its cycle. Parts refuse work
  /// on an unfinished furnace, because a hearth loaded before the walls are up is a charge the player
  /// cannot get back out.</summary>
  protected bool FurnaceComplete => Core?.StructureComplete == true;

  /// <summary>The animation cache key: unique per part <b>and per orientation</b>, or the four rotations
  /// of one part share a joint hierarchy and only the first-built one poses correctly.</summary>
  protected virtual string AnimCacheKey =>
    Block.Code.Path + "-" + (Block.Variant["side"] ?? "north");

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    _toggle = new ToggleAnimator(this, BuildAnimator);
    _toggle.Initialize(ApplyPose);
  }

  /// <summary>
  /// Loads the block's own shape and initialises the animator on it. Shared verbatim across the parts -
  /// they differ only in the cache key, which <see cref="AnimCacheKey"/> supplies.
  /// </summary>
  private void BuildAnimator(BEBehaviorAnimatable animatable)
  {
    var capi = (ICoreClientAPI)Api;
    Shape? shape = capi
      .Assets.TryGet(
        Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")
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
  /// Selects the part's pose from its own state. Must route through <see cref="Pose"/>. The default is a
  /// no-op, because the hearths have <b>no animations at all</b> - their shape is static and only its
  /// contents change, so they draw through a per-block-entity <c>OnTesselation</c> instead. The base is
  /// still worth sharing with them for the furnace link.
  /// </summary>
  protected virtual void ApplyPose() { }

  /// <summary>Runs <paramref name="pose"/> only when a real animator exists (client, shape resolved).</summary>
  protected void Pose(System.Action<BlockEntityAnimationUtil> pose) => _toggle?.Pose(pose);

  /// <summary>Re-poses after a wrench rotation, which rebuilds the block under a new orientation.</summary>
  public override void OnExchanged(Block block)
  {
    base.OnExchanged(block);
    _toggle?.Rebuild();
    ApplyPose();
  }

  #endregion

  /// <summary>
  /// Starts <paramref name="clip"/> and stops every other clip in <paramref name="group"/> - the shape of
  /// every toggle here (open or shut, never both). Passing null for <paramref name="clip"/> stops them all.
  /// Poses ease rather than snap, because these are doors and levers a player watches move.
  /// </summary>
  protected void PoseOneOf(string? clip, params string[] group) =>
    Pose(util =>
    {
      foreach (string other in group)
        if (other != clip)
          util.StopAnimation(other);
      if (clip != null)
        util.StartAnimation(
          new AnimationMetaData
          {
            Animation = clip,
            Code = clip,
            AnimationSpeed = 1.5f,
            EaseInSpeed = 6f,
            EaseOutSpeed = 6f,
          }.Init()
        );
    });
}
