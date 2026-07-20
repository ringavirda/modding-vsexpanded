using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The cold blast furnace's core - the anchor block of the furnace multiblock, sitting at the centre
/// of the lowest layer directly under the hearth column. Routes Ctrl + Shift + right-click to the
/// <see cref="BlockEntityBlastFurnaceCold"/> for the build outline while the structure is incomplete.
/// </summary>
[BlockRegister]
public partial class BlockBlastFurnaceCoreCold
  : BlockFurnaceCoreBase,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The cold blast-furnace core blocktype. The anchor of the whole furnace multiblock: its
  /// 157-cell structure map is drawn as nine ASCII cross-sections (one per Y level, y=0 the hearth floor
  /// the core itself sits in, up to y=8 the open stack).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(domain, "blastfurnacecore", "furnaces/blastfurnace-core")
        .Class<BlockBlastFurnaceCoreCold>()
        .EntityClass<BlockEntityBlastFurnaceCold>()
        .Texture("all", "game:block/clay/refractory/tier1/front1")
        // The furnace footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X,
        // origin x=-3/z=-2 so the core lands on the layout's own (0,0,0)). y=0 the hearth floor ..
        // y=8 the open top, which IS the cold furnace's chimney - it takes no exhaust outlets.
        // Legend: # refractory brick, C the core (origin), T tap, Y tuyere, H tall hopper,
        // f structure filler, c coal/air, a air. Compared as an unordered cell set by
        // DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('C', "iwex:blastfurnacecore-*")
            .Legend('T', "iwex:moltenmetaltap*")
            .Legend('Y', "iwex:tuyere*")
            .Legend('H', "iwex:hopper-tall*")
            .Legend('f', "exlib:structurefiller")
            .Legend('c', "@(air|coalpile)")
            .Legend('a', "game:air")
            .Layer(
              0,
              """
              . . # . # .
              # # # # # #
              # # # C # #
              # # # # # #
              . . # . # .
              """
            )
            .Layer(
              1,
              """
              . . # . # .
              # # # Y # #
              . # # c c T
              # # # Y # #
              . . # . # .
              """
            )
            .Layer(
              2,
              """
              . . # # # .
              # # c c c #
              . T c c c #
              # # c c c #
              . . # # # .
              """
            )
            .Layer(
              3,
              """
              . . # # # .
              # # c c c #
              . # c c c #
              # # c c c #
              . . # # # .
              """
            )
            .Layer(
              4,
              """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """
            )
            .Layer(
              5,
              """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """
            )
            .Layer(
              6,
              """
              . . . # . .
              . . # # # .
              . # H a # #
              . . # # # .
              . . . # . .
              """
            )
            .Layer(
              7,
              """
              . . . . . .
              . . # # # .
              . . f a # .
              . . # # # .
              . . . . . .
              """
            )
            .Layer(
              8,
              """
              . . . . . .
              . . . # . .
              . . . a # .
              . . . # . .
              . . . . . .
              """
            )
        ),
    ];

  #endregion
}
