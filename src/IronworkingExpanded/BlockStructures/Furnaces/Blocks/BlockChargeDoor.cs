using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The work door of a reverberatory furnace - the opening the charge goes in through and the finished
/// work comes out of. Two blocktypes come off this one class because they are the same door doing
/// different jobs:
/// <list type="bullet">
/// <item><b><c>chargedoor</c></b> - the plain door: the heating furnace's, and the beehive coke oven's
/// drawing door. Open or shut, nothing else.</item>
/// <item><b><c>puddlingchargedoor</c></b> - the same frame with a <b>second, smaller door</b> and the
/// puddler's tools racked on it. The small door is the point: a puddler works the bath through a
/// hand-sized opening precisely so the heat does not pour out of the big one every time he rabbles.</item>
/// </list>
/// <para>
/// The door is <b>two cells tall</b> - a door a man walks a bloom through is not one metre high - so it
/// is a mega-block with a single filler above it. Interactions on that filler route back down here, which
/// is what lets a player click anywhere on the visible door rather than hunting for its bottom half.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockChargeDoor
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      // The plain door. Clip names are carried as attributes rather than hard-coded, because the two
      // doors' shapes were authored with different clip names ("open" vs "open-main") and the block
      // entity must not need to know which blocktype it is running as.
      Door(domain, "chargedoor", "furnace/chargedoor")
        .Attribute("doorClips", new { main = "open", mainShut = "closed" })
        .FillerOffsets(UpperHalf),
      // The puddling door: the same frame plus the small working door.
      Door(domain, "puddlingchargedoor", "furnace/puddlingchargedoor")
        .Attribute(
          "doorClips",
          new
          {
            main = "open-main",
            mainShut = "closed-main",
            small = "open-small",
          }
        )
        .FillerOffsets(UpperHalf),
      // The charge lid - the same chassis laid flat. A beehive coke oven is charged through its crown
      // and then has to be sealed: coking is destructive distillation, so coal heated with access to air
      // burns instead of coking. The tall hopper that does the charging is open storage with an open top,
      // so the crown needs something above it that actually closes. The draft crucible furnace wants the
      // same part over its pot chamber.
      // Third blocktype, ~no new code: the shape's two clips are named like the plain door's, so
      // `doorClips` is a straight copy and BlockEntityChargeDoor serves it unchanged.
      Lid(domain, "chargelid", "furnace/chargelid")
        .Attribute("doorClips", new { main = "open", mainShut = "closed" }),
    ];

  /// <summary>
  /// The door's upper half. A door a man walks a bloom through is not one metre high, so the frame is two
  /// cells tall and the top one needs real collision or a player walks through a shut door.
  /// <para>
  /// Declared per-def rather than inside <see cref="Door"/>, because the <b>lid does not get one</b>:
  /// it is 4/16 of a single cell, and a phantom filler above it would both block the cell a player charges
  /// through and unbalance the furnace layouts' filler accounting (every declared <c>f</c> cell must have
  /// a producer, or the structure can never complete).
  /// </para>
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
  /// cell - so it shares <see cref="Door"/> and simply never calls <c>FillerOffsets</c>.
  /// <para>
  /// <b>Its facing is load-bearing</b>, unlike the firebox's. The frame is asymmetric in z (a short front
  /// rail against a taller back rail with an under-lip), so a lid laid the wrong way round is visibly
  /// wrong - which is exactly what a layout's orientation check is for.
  /// </para>
  /// </summary>
  private static ExBlockDef Lid(string domain, string type, string shape) =>
    Door(domain, type, shape)
      // A lid is 4/16 tall shut. The default full cube would have the player standing a metre above the
      // crown they are charging through.
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
      // The build outline: a door is a functional cell of the furnace layout, so a player standing at it
      // can preview and complete an unfinished furnace. Before any other right-click consumer.
      .Behavior("MultiblockStructure")
      .Behavior("ExOrientable")
      // `type` names the family member; every furnace part shares the code `iwex:furnace`
      // (see BlockFurnaceCoreBase.FurnaceCode and N7).
      .VariantGroup("type", type)
      .SideVariant()
      .ShapeByTypePerOrientation("iwex:" + shape, 0)
      .CreativeCommon("*-n")
      .Replaceable(400)
      .Resistance(3.5f)
      .LightAbsorption(3)
      .Sound("walk", "walk/stone")
      .Sound("place", "block/ceramicplace")
      .SoundByTool(EnumTool.Pickaxe, "block/rock-hit-pickaxe", "block/rock-break-pickaxe")
      .SolidNonOpaque();

  #endregion

  /// <summary>The footprint turns with the door, so the filler always lands on the door's own top half.</summary>
  public override int StructureAngle =>
    ExpandedLib.Helpers.ExOrientation.AngleFromSide(Variant["side"]);

  #region Interaction

  private bool HandleInteract(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
  {
    if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityChargeDoor be)
      return false;

    // Consume the build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace
    // instead of swinging the door.
    if (BlockBehaviorMultiblockStructure.TryToggleProjection(world, byPlayer, pos))
      return true;

    if (world.Side == EnumAppSide.Server)
    {
      // Sneak works the small door where there is one - the whole reason it exists is that you open it
      // without opening the big one. On the plain door sneak falls through to the main door, so the
      // gesture never does nothing.
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

  private WorldInteraction[] DoorHelp(IWorldAccessor world, BlockPos pos)
  {
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:chargedoor-help-toggle",
        MouseButton = EnumMouseButton.Right,
      },
    };
    if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChargeDoor { HasSmallDoor: true })
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:chargedoor-help-small",
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
