using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The damper at the top of a puddling furnace's chimney, thrown by the control rod running down its side.
/// <para>
/// This is the furnace's <b>only</b> air control. A reverberatory furnace has no blower - the chimney's own
/// natural draught pulls the air across the hearth - so the one thing a puddler can vary is how far he
/// opens the damper. That makes it the counterpart of the blast furnace's tuyeres: same job, opposite end
/// of the machine, and no pipe network involved.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingChimneyCap : BlockEntityFurnacePart
{
  /// <summary>Whether the damper stands open, letting the stack pull.</summary>
  public bool IsOpen { get; private set; }

  /// <summary>Throws the damper.</summary>
  public void Toggle()
  {
    IsOpen = !IsOpen;
    MarkDirty(true);
    ApplyPose();
  }

  // "idle" is the closed rest pose and must be held, not stopped: the shape is drawn by the animator, so
  // with no clip running the cap would simply vanish. That is the same trap the RCC machines hit.
  protected override void ApplyPose() => PoseOneOf(IsOpen ? "open" : "idle", "idle", "open");

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("damperOpen", IsOpen);
  }

  public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
  {
    base.FromTreeAttributes(tree, worldForResolving);
    bool was = IsOpen;
    IsOpen = tree.GetBool("damperOpen");
    if (Api?.Side == EnumAppSide.Client && was != IsOpen)
      ApplyPose();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
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
