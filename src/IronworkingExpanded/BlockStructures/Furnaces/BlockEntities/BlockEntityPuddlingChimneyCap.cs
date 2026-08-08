using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The damper at the top of a puddling furnace's chimney, thrown by the control rod running down its
/// side. It is the furnace's only air control: a reverberatory furnace has no blower and no pipe
/// network, and air is pulled across the hearth by the chimney's own natural draught, so the damper
/// opening is the only variable.
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingChimneyCap : BlockEntityFurnacePart {
  /// <summary>Whether the damper stands open, letting the stack pull.</summary>
  public bool IsOpen { get; private set; }

  /// <summary>Throws the damper open or shut.</summary>
  public void Toggle() {
    IsOpen = !IsOpen;
    MarkDirty(true);
    ApplyPose();
  }

  // "idle" is the closed rest pose and must be held, not stopped: the shape is drawn by the animator, so
  // with no clip running the cap would not render.
  protected override void ApplyPose() =>
    PoseOneOf(IsOpen ? "open" : "idle", "idle", "open");

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("damperOpen", IsOpen);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    bool was = IsOpen;
    IsOpen = tree.GetBool("damperOpen");
    if (Api?.Side == EnumAppSide.Client && was != IsOpen)
      ApplyPose();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "iwex:chimneycap-state",
        Lang.Get(IsOpen ? "iwex:chargedoor-open" : "iwex:chargedoor-closed")
      )
    );
    if (Core is null)
      dsc.AppendLine(Lang.Get("iwex:furnacepart-nofurnace"));
  }

  #endregion
}
