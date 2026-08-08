using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using LowPressureExpanded;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.MathTools;

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
      Core(domain, "blastfurnacecore", type: null, "blastfurnace/core")
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
        // y=8 the reinforced charging hopper. Legend: # refractory brick, C the core (origin), T the
        // iron tap, S the slag tap, Y/y the north/south tuyere, P pipe outlet, B bell hopper,
        // R reinforced hopper, c coal/air, p the crucible floor (same code as c), a air. Compared as an
        // unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            // Tier 3 exactly, not VanillaCodes.Refractory: the hot blast leaves no cheaper tier viable,
            // and this is the one shell in the family that pins its material.
            .Legend('#', VanillaCodes.RefractoryTier(3))
            .Legend('C', SmexBlocks.BlastfurnaceCore.Any)
            // Facing-pinned, not `.Any`, for the same reason the tuyeres below are: a wildcarded
            // facing lets a tap be fitted the wrong way round and still complete the structure, leaving a
            // furnace that builds, lights, melts and then silently will not drain.
            // Caution: the letter reads backwards. `BlockEntityFurnaceTap.TryPourMetal` spouts at
            // `Pos.AddCopy(facing.Opposite).DownCopy()`, so a tap faces into the furnace and drains out
            // the other way. `T` sits in the east wall at (2,1,0) and is declared west, so it pours to
            // (3,0,0) - outside the drawn grid, free by accident of the footprint.
            .Legend('T', IwexBlocks.FurnaceIrontap.WithSide(BlockFacing.WEST))
            // The slag tap - its own block since iwex typed the taps, so this drawing states which notch
            // goes where. See iwex's cold furnace.
            // `S` sits in the west wall at (-2,2,0) and is declared east, so it pours to (-3,1,0) -
            // the `.` at the west end of layer 1's z=0 row. That cell is its runout: claim it back and
            // the cinder notch has nowhere to drain, silently - the structure still completes and the
            // furnace still lights, it just never sheds its slag.
            .Legend('S', IwexBlocks.FurnaceSlagtap.WithSide(BlockFacing.EAST))
            // The blast inlets, orientation-pinned rather than wildcarded, which is why there are two
            // glyphs for what is one block. A tuyere is walled in on three sides, so its cell admits exactly
            // one connector face - north out of the north wall, south out of the south - and a placed node
            // is exchanged onto that face by BlockNetworkNode.RecalculateAndSyncOrientations as soon as the
            // brick goes up. So the drawing can state it, and `MultiblockFacings` rotates the letter with
            // the structure. A wildcarded legend (`iwex:furnace-tuyere-*`) would let a tuyere fitted facing
            // into the hearth complete the furnace exactly as a correct one does.
            .Legend('Y', IwexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('y', IwexBlocks.FurnaceTuyere.WithOrientation("s"))
            .Legend('P', LpexCodes.PipeOutlet)
            .Legend('R', SmexBlocks.BlastfurnaceHopperreinforced.Code)
            .Legend('B', SmexBlocks.BlastfurnaceHopperbell.Code)
            .Legend('c', IwexCodes.ChargeShaft)
            // The crucible floor - its own glyph so it can carry the pool role beside the burden one.
            // `HearthCell`, not `ChargeShaft`. The two differ by the hearth-metal code: the furnace's
            // own pool stands on this course, and a cell whose glyph does not admit
            // `iwex:hearthmetal-*` is a cell the structure rejects the moment metal appears in it -
            // built, lit, run, and then broken by its own extinguish path.
            .Legend('p', IwexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column - see iwex's cold furnace for why the role rather than the legend
            // string answers "where does charge stand". Stated on this drawing rather than inherited.
            // Note: this drawing does not mirror the cold furnace's cells. The cold furnace's crucible
            // spans the full hearth course (39 cells) and its cinder notch sits level with its iron
            // one, while this drawing keeps the narrow two-cell crucible and the high slag tap.
            // Whether the hot furnace should follow is a design question for the smex remake, not
            // something to "fix" by copying numbers across.
            .Role('c', CellRole.Chargeable)
            // Burden and pool at once, exactly as the cold furnace's crucible is.
            .Role('p', CellRole.Chargeable)
            .Role('p', CellRole.Pool)
            .Role('Y', CellRole.Tuyere)
            .Role('y', CellRole.Tuyere)
            // The only furnace of the three that vents. Its outlets are the one cell set the cold
            // furnace and the cupola answer empty for, and the layout is now what says so on all three -
            // an override declaring "no outlets" in C# is no longer needed to state the absence.
            .Role('P', CellRole.GasOutlet)
            // The two drains. Not the same cells as the cold furnace's - see the note on the
            // burden column above. The iron notch agrees; the cinder notch does not.
            .Role('T', CellRole.MetalTap)
            .Role('S', CellRole.SlagTap)
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
              . . # p p T
              # . # y # #
              . . . . # .
              """
            )
            .Layer(
              2,
              """
              . . # # # .
              # # c c c #
              . S c c c #
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
