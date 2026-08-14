using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.CowperStove.Blocks;

/// <summary>
/// Intake and anchor block of the cowper-stove multiblock. Routes exhaust gas into the stove. The
/// build-outline projection (Ctrl + Shift + right-click) comes from the shared
/// <c>MultiblockStructure</c> block behavior declared in the definition below.
/// </summary>
[BlockRegister]
public partial class BlockCowperStoveIntake
  : Block,
    INetworkConnector,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The cowper-stove intake blocktype: the anchor of the cowper multiblock. Its 59-cell
  /// structure map is drawn as one top-down ASCII cross-section per Y level (y=-1 the brick
  /// foundation up to y=5 the domed cap), compared as an unordered cell set by
  /// <c>DefinitionParity</c>.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "cowperstove", "cowperstove/intake")
        .Class<BlockCowperStoveIntake>()
        .EntityClass<BlockEntityCowperStove>()
        .Material(EnumBlockMaterial.Ceramic)
        .Sound("walk", "game:walk/stone")
        .Sound("place", "game:block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "game:block/rock-hit-pickaxe",
          "game:block/rock-break-pickaxe"
        )
        .MaxStackSize(1)
        .Handbook("cowperstove-intake-*")
        // The stove footprint, one top-down cross-section per Y level (rows +Z, cols +X, origin
        // x=-1/z=0), from y=-1 the brick foundation to y=5 the domed cap. Legend: # refractory
        // brick, I the intake (origin), P pipe outlet, X pipe passthrough, H heat sink, D coke-oven
        // door, a air, c coal/air. Compared as an unordered cell set, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-1, 0)
            .Legend('#', VanillaCodes.Refractory)
            .Legend('I', SmexBlocks.CowperstoveIntake.Any)
            .Legend('P', IiexCodes.PipeOutlet)
            .Legend('X', IiexCodes.PipePassthroughAny)
            .Legend('H', SmexBlocks.CowperstoveHeatsink.Any)
            .Legend('D', VanillaCodes.Sealing(BlockFacing.WEST))
            .Legend('a', VanillaCodes.Air)
            .Legend('c', VanillaCodes.CoalBed)
            .Layer(
              -1,
              """
              # c #
              # # #
              # # #
              """
            )
            .Layer(
              0,
              """
              # I #
              D H #
              # P #
              """
            )
            .Layer(
              1,
              """
              # P #
              # H #
              # X #
              """
            )
            .Layer(
              2,
              """
              # # #
              # H #
              # # #
              """
            )
            .Layer(
              3,
              """
              # # #
              # H #
              # # #
              """
            )
            .Layer(
              4,
              """
              # # #
              # a #
              # # #
              """
            )
            .Layer(
              5,
              """
              . # .
              # # #
              . # .
              """
            )
        )
        .CreativeCommon("*-intake-*-s")
        .Behavior("MultiblockStructure")
        .Behavior("Lockable")
        .Behavior("ExOrientable")
        .VariantGroup("type", "intake")
        .VariantGroup("refractory", "tier1", "tier2", "tier3")
        .SideVariant()
        .ShapeByType("*-intake-*-n", "smex:cowperstove/intake", rotateY: 0)
        .ShapeByType("*-intake-*-w", "smex:cowperstove/intake", rotateY: 90)
        .ShapeByType("*-intake-*-s", "smex:cowperstove/intake", rotateY: 180)
        .ShapeByType("*-intake-*-e", "smex:cowperstove/intake", rotateY: 270)
        .Texture("front1", "game:block/clay/refractory/{refractory}/front1")
        .SideSolid(true)
        .SideOpaque(false),
    ];

  #endregion

  public string NetworkType => "pipe";

  /// <summary>The exhaust connector sits on the intake's local-south face, rotated with the block.</summary>
  public bool HasConnectorAt(BlockFacing face) =>
    face
    == ExOrientation.RotateFacing(
      BlockFacing.SOUTH,
      ExOrientation.AngleFromSide(Variant["side"])
    );

  /// <summary>Includes the refractory tier in the display name.</summary>
  public override string GetHeldItemName(ItemStack itemStack) =>
    ExBlockNames.Decorate(this, base.GetHeldItemName(itemStack));
}
