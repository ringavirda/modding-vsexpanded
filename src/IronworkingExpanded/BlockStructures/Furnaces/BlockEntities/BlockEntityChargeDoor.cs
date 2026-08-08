using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// A reverberatory furnace's work door. Holds two independent open/shut flags - the main door and, on the
/// puddling variant, the small working door the charge is rabbled through without dumping the heat - and
/// poses the shape to match.
/// </summary>
[BlockEntityRegister]
public class BlockEntityChargeDoor : BlockEntityFurnacePart {
  /// <summary>Whether the main door stands open.</summary>
  public bool MainOpen { get; private set; }

  /// <summary>Whether the small working door stands open. Always false on a door that has none.</summary>
  public bool SmallOpen { get; private set; }

  /// <summary>True when this blocktype was authored with a small working door (the puddling variant).</summary>
  public bool HasSmallDoor => SmallClip != null;

  // Clip names live in block attributes because the two door shapes were authored with different ones
  // ("open" vs "open-main"). Reading them here keeps one block entity serving both.
  private string MainClip =>
    Block.Attributes?["doorClips"]?["main"]?.AsString("open") ?? "open";
  private string MainShutClip =>
    Block.Attributes?["doorClips"]?["mainShut"]?.AsString("closed") ?? "closed";
  private string? SmallClip =>
    Block.Attributes?["doorClips"]?["small"]?.AsString(null);

  /// <summary>
  /// True while either door stands open; any degree of opening lets the draught through. Written for the
  /// furnace core's damper model, which does not read it yet.
  /// </summary>
  public bool IsVenting => MainOpen || SmallOpen;

  #region Interaction

  /// <summary>Swings the main door.</summary>
  public void ToggleMain() {
    MainOpen = !MainOpen;
    MarkDirty(true);
    ApplyPose();
  }

  /// <summary>Swings the small working door. No-op on a door that has none.</summary>
  public void ToggleSmall() {
    if (!HasSmallDoor)
      return;
    SmallOpen = !SmallOpen;
    MarkDirty(true);
    ApplyPose();
  }

  #endregion

  #region Pose

  protected override void ApplyPose() {
    // The main door's two states are separate clips rather than one played backwards, so they are
    // mutually exclusive - running both leaves the door easing between two poses forever.
    PoseOneOf(MainOpen ? MainClip : MainShutClip, MainClip, MainShutClip);

    // The small door has an open clip only; shut is the clip stopped. It is independent of the main door,
    // so the small one can stand open while the main one is shut.
    if (SmallClip is { } small)
      Pose(util => {
        if (SmallOpen)
          util.StartAnimation(
            new AnimationMetaData {
              Animation = small,
              Code = small,
              AnimationSpeed = 1.5f,
              EaseInSpeed = 6f,
              EaseOutSpeed = 6f,
            }.Init()
          );
        else
          util.StopAnimation(small);
      });
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("mainOpen", MainOpen);
    tree.SetBool("smallOpen", SmallOpen);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    bool wasMain = MainOpen;
    bool wasSmall = SmallOpen;
    MainOpen = tree.GetBool("mainOpen");
    SmallOpen = tree.GetBool("smallOpen");
    // Re-pose only on a real change: FromTree runs on every sync, and re-starting a clip that is already
    // running restarts its ease and makes the door twitch.
    if (
      Api?.Side == EnumAppSide.Client
      && (wasMain != MainOpen || wasSmall != SmallOpen)
    )
      ApplyPose();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "iwex:chargedoor-state",
        Lang.Get(MainOpen ? "iwex:chargedoor-open" : "iwex:chargedoor-closed")
      )
    );
    if (HasSmallDoor)
      dsc.AppendLine(
        Lang.Get(
          "iwex:chargedoor-state-small",
          Lang.Get(
            SmallOpen ? "iwex:chargedoor-open" : "iwex:chargedoor-closed"
          )
        )
      );
    if (Core is null)
      dsc.AppendLine(Lang.Get("iwex:furnacepart-nofurnace"));
  }

  #endregion
}
