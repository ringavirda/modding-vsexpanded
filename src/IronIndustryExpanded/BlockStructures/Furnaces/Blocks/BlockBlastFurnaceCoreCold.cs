using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The cold blast furnace's core - the anchor block of the furnace multiblock, sitting at the centre
/// of the lowest layer directly under the hearth column. Routes Ctrl + Shift + right-click to the
/// <see cref="BlockEntityBlastFurnaceCold"/> for the build outline while the structure is incomplete.
/// </summary>
[BlockRegister]
public partial class BlockBlastFurnaceCoreCold
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The cold blast-furnace core blocktype, anchor of the whole furnace multiblock. Its 160-cell
  /// structure map is drawn as nine ASCII cross-sections, one per Y level, from y=0 (the hearth floor the
  /// core sits in) to y=8 (the open stack).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(
          domain,
          FurnaceCode,
          "blastcore",
          "furnace/blastcore",
          "tier1",
          "tier2",
          "tier3"
        )
        .Class<BlockBlastFurnaceCoreCold>()
        .EntityClass<BlockEntityBlastFurnaceCold>()
        // A refractory-brick cube in whichever tier it was built from ({tier}): plain brick on the side
        // faces, the orientation marker on the north face and the "BF/C" (blast furnace, cold) type label
        // on the south, so the anchor reads apart from the hot core and the cupola and shows its facing.
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/bfc"
        )
        // The furnace footprint, one top-down cross-section per Y level (rows +Z, cols +X, origin
        // x=-3/z=-2 so the core lands on the layout's own (0,0,0)). y=0 is the hearth floor, y=8 the open
        // top, which is the cold furnace's chimney: it takes no exhaust outlets.
        // Legend: # refractory brick, C the core (origin), I the iron tap, S the slag tap, Y/T the
        // north/south tuyere, H tall hopper, f structure filler, c the shaft, h the crucible floor (same
        // code as c), a air.
        // Compared as an unordered cell set by DefinitionParity, so the block numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            .Legend('#', VanillaCodes.Refractory)
            .Legend(
              'C',
              IiexBlocks.FurnaceBlastcore.WithSide(BlockFacing.NORTH)
            )
            .Legend('I', IiexBlocks.FurnaceIrontap.WithSide(BlockFacing.WEST))
            // The slag tap is a separate block from the iron tap, so the two glyphs carry different
            // codes and the drawing states which hole goes where. One shared code told apart only by
            // role would let either notch be built in the other's cell and still complete.
            // A tap's declared facing is the opposite of the direction it pours:
            // BlockEntityFurnaceTap.TryPourMetal targets Pos.AddCopy(facing.Opposite).DownCopy(). So `I`
            // in the east wall is declared WEST and pours to (3,0,0), and `S` in the west wall is
            // declared EAST and pours to (-3,0,0).
            // (-3,0,0) is why layer 0's z=0 row opens with `.` rather than `#`: the slag tap's runout
            // lands inside the drawn grid, so that cell is left unclaimed as a runout notch in the
            // hearth base. Claiming it leaves the slag tap nowhere to pour - the structure still
            // completes and the furnace still lights, but the cinder never drains.
            .Legend('S', IiexBlocks.FurnaceSlagtap.WithSide(BlockFacing.EAST))
            // The blast inlets: one block, two glyphs, because the legend pins orientation rather than
            // wildcarding it. A tuyere is walled in on three sides, so its cell admits exactly one
            // connector face and BlockNetworkNode.RecalculateAndSyncOrientations exchanges a placed node
            // onto that face as soon as the brick goes up; MultiblockFacings rotates the letter with the
            // structure. A wildcarded legend would accept a tuyere facing into the hearth.
            // The letter names the face the connector sits on, so each tuyere points out at the player's
            // pipe: `Y` is drawn in the north row (z=-1) and connects `n`, `T` in the south row and
            // connects `s`. Swapping them is unbuildable - a pipe neighbour makes that face required in
            // ComputeValidOrientations, so the placed tuyere is exchanged onto it and the cell demanding
            // the opposite letter can never be satisfied.
            .Legend('Y', IiexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('T', IiexBlocks.FurnaceTuyere.WithOrientation("s"))
            // No passthrough legend: the tuyere sits at (0,2,±2), the outermost cell, so the player's pipe
            // butts straight against it. A tuyere buried in the brick would need a claimed cell to carry
            // the blast through the wall.
            .Legend('H', IiexBlocks.HopperTall.WithSide(BlockFacing.EAST))
            .Legend('f', ExCodes.Filler)
            // ChargeShaft's domain wildcard is load-bearing: a bare alternation puts a lit furnace out the
            // moment the player charges it. See IiexCodes.ChargeShaft.
            .Legend('c', IiexCodes.ChargeShaft)
            // The crucible floor: the same code as the shaft above it, given its own glyph only so it can
            // carry a second role. Two glyphs on one code share one block number, so the structure
            // requires nothing extra. See MultiblockLayoutBuilder.Build.
            .Legend('h', IiexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column, named on the drawing rather than derived from the legend string:
            // "which cells accept an iiex:furnace-chargepile" gives the same 36 cells only while the
            // shaft glyph keeps `chargepile` in its alternation, and nothing checks that coupling.
            // `a` is air as well and is not chargeable - it is the vent shaft above the stockline.
            .Role('c', CellRole.Chargeable)
            // The crucible is both burden and pool: charge rests on it while the furnace runs and molten
            // iron freezes onto it when the furnace is put out. The two occupy the same cells, so one
            // glyph carries both roles.
            .Role('h', CellRole.Chargeable)
            .Role('h', CellRole.Pool)
            // The blast intake. The role lets the furnace read its tuyere positions off its own drawing
            // instead of carrying a copy of these two offsets.
            .Role('Y', CellRole.Tuyere)
            .Role('T', CellRole.Tuyere)
            // The two drains, named on the drawing rather than as Vec3i literals in the block entity.
            // Both roles are [SingleCell], so the build refuses a layout that draws either glyph twice and
            // the consumer can read a point instead of a set.
            .Role('I', CellRole.MetalTap)
            .Role('S', CellRole.SlagTap)
            .Layer(
              0,
              """
              . . # # # .
              # # # # # #
              . # # C # #
              # # # # # #
              . . # # # .
              """
            )
            .Layer(
              1,
              """
              . . # # # .
              # # # # # #
              . S h h h I
              # # # # # #
              . . # # # .
              """
            )
            .Layer(
              2,
              """
              . . # Y # .
              # # c c c #
              . # c c c #
              # # c c c #
              . . # T # .
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
