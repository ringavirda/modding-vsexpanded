using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace IronIndustryExpanded.BlockStructures.Crafting.Blocks;

/// <summary>
/// The workbench: a two-cell bench for assemblies too large for the player's own grid. Right-click
/// anywhere on it opens the bench window (<see cref="BlockEntityWorkbench"/>), whose 5x5 grid takes
/// ordinary grid recipes and whose output is an inventory rather than a slot, so a craft yielding more
/// than one stack has somewhere to land. See docs/design/machines/workbench.md.
/// <para>
/// Authored in the frame the art is drawn in: the bench body runs +X from the principal and the vices
/// mount on its +Z edge, which is the side the player works from. That edge is the <c>s</c> variant, so
/// both the shape and the footprint carry a +180 offset over
/// <see cref="ExOrientation.AngleFromSide"/> and the drawn frame is the one placed when the player
/// stands south of it.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockWorkbench
  : BlockFilledMegastructure,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The workbench blocktype: four horizontal orientations off <c>ExOrientable</c>, one shape
  /// spun per orientation, and the two-cell footprint below.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "crafting-workbench", "crafting/workbench")
        .Class<BlockWorkbench>()
        .EntityClass<BlockEntityWorkbench>()
        .Behavior("ExOrientable")
        .SideVariant()
        .CreativeCommon("*-n")
        .ShapeSpunPerOrientation("iiex:crafting/workbench", 180)
        .Material(EnumBlockMaterial.Wood)
        .MaxStackSize(1)
        .Resistance(3f)
        .Sound("walk", "game:walk/wood")
        .Sound("place", "game:block/planks")
        .MaterialDensity(600)
        .FillerOffsets(Footprint)
        // The bench stands open under its top, so the placed cell must not cull the faces around it.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint

  /// <summary>
  /// The one cell the bench reserves besides its principal: the far half of the bench top, at
  /// <c>x=+1</c> in the drawn frame. The owner's layout is <c>O #</c> - a plain full filler, since the
  /// bench has a single interaction and it reaches the whole block through the filler's own forwarding.
  /// </summary>
  private static readonly IReadOnlyList<FillerCellSpec> Footprint =
    StructureFootprint.Layout(f =>
      // One XY row at z=0; columns run +X from the principal.
      f.Face(
        0,
        """
        O #
        """
      )
    );

  /// <inheritdoc/>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant?["side"]) + 180;

  #endregion

  #region Interaction

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityWorkbench bench
    ) {
      bench.OnInteract(byPlayer);
      return true;
    }

    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    return baseHelp
      .Append(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-workbench-open",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .ToArray();
  }

  #endregion
}
