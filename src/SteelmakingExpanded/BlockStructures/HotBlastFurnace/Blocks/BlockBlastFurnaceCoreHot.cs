using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;

/// <summary>
/// The hot blast furnace's core - the anchor block of the furnace multiblock, sitting at the centre
/// of the lowest layer directly under the hearth column. Routes Ctrl + Shift + right-click to the
/// <see cref="BlockEntityBlastFurnaceHot"/> for the build outline while the structure is incomplete.
/// </summary>
[BlockRegister]
public partial class BlockBlastFurnaceCoreHot
  : BlockFurnaceCoreBase,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The hot blast-furnace core blocktype. Same shape and anchor rules as the cold core, but
  /// tier-3 refractory only - the hot blast is the one furnace that will not take a lower brick - and
  /// its shaft carries the exhaust outlets + bell/reinforced hoppers the cold furnace does without.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(domain, "blastfurnacecore", "blastfurnace/core")
        .Class<BlockBlastFurnaceCoreHot>()
        .EntityClass<BlockEntityBlastFurnaceHot>()
        // A tier-3 refractory-brick cube: plain brick on the side faces, the orientation marker on the
        // north face and the "BF/H" (blast furnace, hot) type label on the south, so the anchor reads
        // apart from the cold core and the cupola - and shows which way it faces - at a glance.
        .Texture("all", "game:block/clay/refractory/tier3/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/tier3/front1",
          "smex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/tier3/front1",
          "smex:block/furnace/bfh"
        )
        // The furnace footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X,
        // origin x=-3/z=-2 so the core lands on the layout's own (0,0,0)). y=0 the hearth floor ..
        // y=8 the reinforced charging hopper. Legend: # refractory brick, C the core (origin), T tap,
        // Y tuyere, P pipe outlet, B bell hopper, R reinforced hopper, c coal/air, a air. Compared as
        // an unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            .Legend('#', "game:refractorybricks-good-tier3")
            .Legend('C', "smex:blastfurnacecore-*")
            .Legend('T', "iwex:moltenmetaltap*")
            .Legend('Y', "iwex:tuyere*")
            .Legend('P', "ppex:pipe-outlet*")
            .Legend('R', "smex:hopperreinforced*")
            .Legend('B', "smex:hopperbell*")
            .Legend('c', "@(air|coalpile)")
            .Legend('a', "game:air")
            .Layer(
              0,
              """
              . . . . # .
              # # # # # #
              # # # C # #
              # # # # # #
              . . . . # .
              """
            )
            .Layer(
              1,
              """
              . . . . # .
              # . # Y # #
              . . # c c T
              # . # Y # #
              . . . . # .
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
              . . . . . .
              . . # P # .
              . . # a # .
              . . # P # .
              . . . . . .
              """
            )
            .Layer(
              7,
              """
              . . . . . .
              . . # # # .
              . . # B # .
              . . # # # .
              . . . . . .
              """
            )
            .Layer(
              8,
              """
              . . . . . .
              . . . # . .
              . . # R # .
              . . . # . .
              . . . . . .
              """
            )
        ),
    ];

  #endregion
}
