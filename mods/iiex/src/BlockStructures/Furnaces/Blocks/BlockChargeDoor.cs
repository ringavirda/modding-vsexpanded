using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The work door of a reverberatory furnace: the opening the charge goes in through and the finished
/// work comes out of. Three blocktypes share this class - <c>chargedoor</c> (the plain door, used by the
/// heating furnace and as the beehive coke oven's drawing door), <c>puddlingchargedoor</c> (the same
/// frame plus a small working door worked without opening the big one) and <c>chargelid</c> (the same
/// chassis laid flat).
/// <para>
/// The door is two cells tall, so it is a mega-block with a single filler above it. Interactions on that
/// filler route back to this block, so a click anywhere on the visible door works.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockChargeDoor
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      // The plain door. Clip names ride as attributes because the two doors' shapes were authored with
      // different names ("open" vs "open-main"), so the block entity need not know which blocktype it
      // is running as.
      Door(domain, "chargedoor", "furnace/chargedoor")
        .Attribute("doorClips", new { main = "open", mainShut = "closed" })
        .FillerOffsets(UpperHalf),
      // The puddling door: the same frame plus the small working door.
      Door(domain, "puddlingchargedoor", "furnace/puddlingchargedoor")
        .Attribute(
          "doorClips",
          // The two working clips are data, not code: the block entity plays whichever name the
          // blocktype gives it, so a door drawn with different ones needs no C#.
          new
          {
            main = "open-main",
            mainShut = "closed-main",
            small = "open-small",
            rabbling = "rabbling",
            paddle = "paddle",
          }
        )
        .FillerOffsets(UpperHalf),
      // The charge lid: the same chassis laid flat, sealing the crown a beehive coke oven is charged
      // through and the draft crucible furnace's pot chamber. Coking is destructive distillation, so
      // the chamber must close, and the tall hopper that charges it has an open top. The shape's clips
      // are named like the plain door's, so BlockEntityChargeDoor serves it unchanged.
      Lid(domain, "chargelid", "furnace/chargelid")
        .Attribute("doorClips", new { main = "open", mainShut = "closed" }),
    ];

  /// <summary>
  /// The door's upper half. The frame is two cells tall and the top cell needs real collision, or a
  /// player walks through a shut door. Declared per-def rather than inside <see cref="Door"/> because
  /// the lid gets none: it is 4/16 of a single cell, and a filler above it would block the cell a player
  /// charges through and unbalance the furnace layouts' filler accounting, where every declared
  /// <c>f</c> cell must have a producer or the structure can never complete.
  /// </summary>
  private static IEnumerable<FillerCellSpec> UpperHalf =>
    StructureFootprint.Layout(f =>
      f.Origin(0, 1)
        .Slice(
          0,
          """
          #
          0
          """
        )
    );

  /// <summary>
  /// The lid: the door chassis laid flat over a charging hole. Everything the door has except the second
  /// cell, so it shares <see cref="Door"/> and never calls <c>FillerOffsets</c>. Its facing is
  /// load-bearing: the frame is asymmetric in z (a short front rail against a taller back rail with an
  /// under-lip), so a layout may orientation-check a lid cell.
  /// </summary>
  private static ExBlockDef Lid(string domain, string type, string shape) =>
    Door(domain, type, shape)
      // A lid is 4/16 tall when shut; the default full cube would stand the player a metre above the
      // crown being charged through.
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.25f, 1f)
      .SingleSelectionBox(0f, 0f, 0f, 1f, 0.25f, 1f);

  private static ExBlockDef Door(string domain, string type, string shape) =>
    ExBlockDef
      .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/" + type)
      .Class<BlockChargeDoor>()
      .EntityClass<BlockEntityChargeDoor>()
      .EntityBehavior("Animatable")
      .Material(EnumBlockMaterial.Ceramic)
      .MaxStackSize(1)
      // Build outline: a door is a functional cell of the furnace layout, so a player standing at it can
      // preview and complete an unfinished furnace. Declared before any other right-click consumer.
      .Behavior("MultiblockStructure")
      .Behavior("ExOrientable")
      // `type` names the family member; every furnace part shares the code `iiex:furnace`
      // (see BlockFurnaceCoreBase.FurnaceCode and N7).
      .VariantGroup("type", type)
      .SideVariant()
      .ShapeByTypePerOrientation("iiex:" + shape, 0)
      .CreativeCommon("*-n")
      .Replaceable(400)
      .Resistance(3.5f)
      .LightAbsorption(3)
      .Sound("walk", "walk/stone")
      .Sound("place", "block/ceramicplace")
      .SoundByTool(
        EnumTool.Pickaxe,
        "block/rock-hit-pickaxe",
        "block/rock-break-pickaxe"
      )
      .SolidNonOpaque();

  #endregion

  /// <summary>The footprint turns with the door, so the filler always lands on the door's own top half.</summary>
  public override int StructureAngle =>
    ExpandedLib.Helpers.ExOrientation.AngleFromSide(Variant["side"]);

  #region Interaction

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pos
  ) {
    if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityChargeDoor be)
      return false;

    // Consume the build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace
    // instead of swinging the door.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(world, byPlayer, pos)
    )
      return true;

    if (world.Side == EnumAppSide.Server) {
      // Sneak works the small door where there is one; on the plain door it falls through to the main
      // door, so the gesture is never inert.
      if (byPlayer.Entity.Controls.ShiftKey && be.HasSmallDoor)
        be.ToggleSmall();
      else
        be.ToggleMain();
    }
    return true;
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) => HandleInteract(world, byPlayer, blockSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => DoorHelp(world, principalSel.Position);

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => DoorHelp(world, selection.Position);

  private WorldInteraction[] DoorHelp(IWorldAccessor world, BlockPos pos) {
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = IiexLang.ChargedoorHelpToggle,
        MouseButton = EnumMouseButton.Right,
      },
    };
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChargeDoor {
        HasSmallDoor: true
      }
    )
      help.Add(
        new WorldInteraction {
          ActionLangCode = IiexLang.ChargedoorHelpSmall,
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "shift",
        }
      );
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChargeDoor be
      && be.ResolveOwningAnchor() is { StructureComplete: false }
    )
      help.AddRange(BlockBehaviorMultiblockStructure.ProjectionHelp(this));
    return [.. help];
  }

  #endregion
}
