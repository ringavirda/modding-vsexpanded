using System.Text;
using ExpandedLib.Blocks.Animation;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity for the blast-furnace tap. Tracks the pouring state (shown via the
/// shape's open animation pose) and accepts molten metal pushed from the furnace to
/// hand down into the canal start beneath it.
/// </summary>
[BlockEntityRegister]
public class BlockEntityMoltenMetalTap : BlockEntity
{
  /// <summary>Whether the tap is currently open and pouring.</summary>
  public bool IsPouring { get; private set; } = false;

  private ToggleAnimator? _toggle;

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    _toggle = new ToggleAnimator(this, BuildAnimator);
    _toggle.Initialize(ApplyPourPose);
  }

  // Non-RCC animated block: load the shape and initialise the animator through the shared toggle helper,
  // which owns the null-animator ready-guard (a failed shape resolve degrades to "not ready", no pose).
  private void BuildAnimator(BEBehaviorAnimatable animatable)
  {
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
      "moltenmetaltap-" + Block.Variant["side"],
      shape,
      capi.Tesselator.GetTextureSource(Block),
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
  }

  /// <summary>Toggles the tap open/closed and updates its pour pose.</summary>
  public void TogglePouring()
  {
    IsPouring = !IsPouring;
    MarkDirty(true);
  }

  private void ApplyPourPose()
  {
    _toggle?.Pose(util =>
    {
      if (IsPouring)
        util.StartAnimation(
          new AnimationMetaData
          {
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

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("isPouring", IsPouring);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    bool prev = IsPouring;
    IsPouring = tree.GetBool("isPouring");
    if (Api?.Side == EnumAppSide.Client && prev != IsPouring)
      ApplyPourPose();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "iwex:tap-state",
        Lang.Get(IsPouring ? "iwex:tap-open" : "iwex:tap-closed")
      )
    );
  }

  #endregion

  #region Pouring

  /// <summary>
  /// Pushes <paramref name="moltenMetal"/> into the canal start beneath the spout.
  /// Returns the amount the canal actually consumed (0 if not pouring or nothing was transferred).
  /// </summary>
  public int TryPourMetal(ItemStack moltenMetal, float temperature)
  {
    if (!IsPouring || moltenMetal == null)
      return 0;

    BlockFacing facing = BlockFacing.FromCode(Block.Variant["side"]);
    if (facing == null)
      return 0;

    BlockPos startPos = Pos.AddCopy(facing.Opposite).DownCopy();

    if (
      Api.World.BlockAccessor.GetBlockEntity(startPos)
      is not BlockEntityMoltenCanalStart startCanal
    )
      return 0;

    // Use the looser predicate so a brim-full start still receives the pour and
    // soaks its heat (keeping it molten) instead of stalling and cooling.
    if (!startCanal.CanReceiveOrSoak(moltenMetal))
      return 0;

    startCanal.BeginFill(Pos.ToVec3d());

    // amount is passed by ref; ReceiveLiquidMetal decrements it by what the
    // network accepted, so after the call it holds the LEFTOVER, not the
    // consumed amount. Return the difference (= actually accepted).
    int requested = moltenMetal.StackSize;
    int amount = requested;
    startCanal.ReceiveLiquidMetal(moltenMetal, ref amount, temperature);
    startCanal.OnPourOver();

    return requested - amount;
  }

  #endregion
}
