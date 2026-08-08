using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Renderers;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity for a shaft-furnace tap-hole, iron notch and cinder notch alike. Tracks the pouring state
/// (shown by the shape's open animation pose) and accepts molten metal pushed from the furnace to hand down
/// into the canal start beneath it.
/// </summary>
[BlockEntityRegister]
public class BlockEntityFurnaceTap : BlockEntity, IMultiblockComponent {
  /// <summary>Whether the tap is currently open and pouring.</summary>
  public bool IsPouring { get; private set; } = false;

  private ToggleAnimator? _toggle;

  // Both taps of a furnace run this block entity; each scans up to its furnace core and shows the pool it
  // drains - the lower (metal) tap the metal, the upper the slag. Which one a tap is is decided by comparing
  // its cell against the core's Metal/SlagTapPos, not by the block type, because the position also answers
  // whether the furnace's drawing marks that cell as a tap at all: a reverberatory hearth marks neither, and
  // a tap stuck on one must show no pool. The same link answers the build-outline projection
  // (ResolveOwningAnchor).
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  /// <summary>The furnace this tap belongs to, resolved by scanning up to the core whose layout owns the
  /// tap's cell (cached and throttled by the link). Drives the pool HUD and the shared build outline.</summary>
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

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _toggle = new ToggleAnimator(this, BuildAnimator);
    _toggle.Initialize(ApplyPourPose);
  }

  // Non-RCC animated block: load the shape and initialise the animator through the shared toggle helper,
  // which owns the null-animator ready guard, so a failed shape resolve degrades to "not ready", no pose.
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

    // The cache key is the block's own rendered code (furnace-irontap-north, ...), not a hand-written
    // string: the two tap types share a shape today but need not always, and a stale key would serve one
    // type's mesh for the other.
    animatable.animUtil.InitializeAnimator(
      Block.Code.Path,
      shape,
      capi.Tesselator.GetTextureSource(Block),
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
  }

  /// <summary>Toggles the tap open/closed and updates its pour pose.</summary>
  public void TogglePouring() {
    IsPouring = !IsPouring;
    MarkDirty(true);
  }

  private void ApplyPourPose() {
    _toggle?.Pose(util => {
      if (IsPouring)
        util.StartAnimation(
          new AnimationMetaData {
            Animation = "open",
            Code = "open",
            AnimationSpeed = 1.5f,
            EaseInSpeed = 6f,
            EaseOutSpeed = 6f,
          }.Init()
        );
      else
        util.StopAnimation("open");
    });
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("isPouring", IsPouring);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    bool prev = IsPouring;
    IsPouring = tree.GetBool("isPouring");
    if (Api?.Side == EnumAppSide.Client && prev != IsPouring)
      ApplyPourPose();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "iwex:tap-state",
        Lang.Get(IsPouring ? "iwex:tap-open" : "iwex:tap-closed")
      )
    );

    // Resolve the furnace this tap drains and show its pool - metal from the lower tap, slag from the upper.
    // A tap with no furnace shows only the open/closed line above, and so does a tap on a furnace whose
    // drawing marks no tap: both Metal/SlagTapPos are null on a reverberatory hearth, which pours nothing.
    if (Anchor.Resolve() is { } core) {
      if (core.MetalTapPos is { } metal && SamePos(Pos, metal))
        core.AppendMoltenMetalInfo(dsc);
      else if (core.SlagTapPos is { } slag && SamePos(Pos, slag))
        core.AppendMoltenSlagInfo(dsc);
    }
  }

  // Coordinate-only equality: the tap and the core's tap cells share a dimension, so comparing X/Y/Z is
  // enough and side-steps BlockPos.Equals's dimension field.
  private static bool SamePos(BlockPos a, BlockPos b) =>
    a.X == b.X && a.Y == b.Y && a.Z == b.Z;

  #endregion

  #region Pouring

  /// <summary>
  /// Pushes <paramref name="moltenMetal"/> into the canal start beneath the spout.
  /// Returns the amount the canal actually consumed (0 if not pouring or nothing was transferred).
  /// </summary>
  public int TryPourMetal(ItemStack moltenMetal, float temperature) {
    if (!IsPouring || moltenMetal == null)
      return 0;

    // ExOrientation.FacingFromSide, not BlockFacing.FromCode: FromCode returns null for a single-letter
    // side token, and a null facing here makes the tap return 0 and never pour, with no error logged.
    BlockFacing? facing = ExOrientation.FacingFromSide(Block.Variant["side"]);
    if (facing == null)
      return 0;

    BlockPos startPos = Pos.AddCopy(facing.Opposite).DownCopy();

    if (
      Api.World.BlockAccessor.GetBlockEntity(startPos)
      is not BlockEntityMoltenCanalStart startCanal
    )
      return 0;

    // The looser predicate, so a brim-full start still receives the pour and soaks
    // its heat (keeping it molten) instead of stalling and cooling.
    if (!startCanal.CanReceiveOrSoak(moltenMetal))
      return 0;

    startCanal.BeginFill(Pos.ToVec3d());

    // amount is passed by ref; ReceiveLiquidMetal decrements it by what the
    // network accepted, so after the call it holds the leftover, not the
    // consumed amount. The difference is what was actually accepted.
    int requested = moltenMetal.StackSize;
    int amount = requested;
    startCanal.ReceiveLiquidMetal(moltenMetal, ref amount, temperature);
    startCanal.OnPourOver();

    return requested - amount;
  }

  #endregion
}
