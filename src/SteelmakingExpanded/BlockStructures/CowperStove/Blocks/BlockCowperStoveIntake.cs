using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.CowperStove.Blocks;

/// <summary>
/// Intake/anchor block of the cowper-stove multiblock. Routes exhaust gas into the
/// stove. The build-outline projection (Ctrl + Shift + right-click) is provided by the
/// shared <c>MultiblockStructure</c> block behavior declared in the block JSON.
/// </summary>
[BlockRegister]
public partial class BlockCowperStoveIntake
  : Block,
    INetworkConnector,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The cowper-stove intake blocktype, authored in C# (migrated from cowperstove/intake.json). The
  /// anchor of the cowper multiblock: its 59-cell structure map is drawn as one top-down ASCII cross-section
  /// per Y level (y=-1 the brick foundation up to y=5 the domed cap), compared as an unordered cell set by
  /// <see cref="DefinitionParity"/>.</summary>
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
        // The stove footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X, origin
        // x=-1/z=0). y=-1 brick foundation .. y=5 the domed cap. Legend: # refractory brick, I the intake
        // (origin), P pipe outlet, X pipe passthrough, H heat sink, D coke-oven door, a air, c coal/air.
        // Compared as an unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-1, 0)
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('I', "smex:cowperstove-intake*")
            .Legend('P', "ppex:pipe-outlet*")
            .Legend('X', "ppex:pipe-passthrough-*")
            .Legend('H', "smex:cowperstoveheatsink*")
            .Legend('D', "game:cokeovendoor*")
            .Legend('a', "game:air")
            .Legend('c', "@(air|coalpile)")
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
        .CreativeCommon("*-intake-*-south")
        .Behavior("MultiblockStructure")
        .Behavior("Lockable")
        .Behavior("HorizontalOrientable")
        .VariantGroup("type", "intake")
        .VariantGroup("refractory", "tier1", "tier2", "tier3")
        .VariantGroupFromProperties("side", "abstract/horizontalorientation")
        .ShapeByType("*-intake-*-north", "smex:cowperstove/intake", rotateY: 0)
        .ShapeByType("*-intake-*-west", "smex:cowperstove/intake", rotateY: 90)
        .ShapeByType("*-intake-*-south", "smex:cowperstove/intake", rotateY: 180)
        .ShapeByType("*-intake-*-east", "smex:cowperstove/intake", rotateY: 270)
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
