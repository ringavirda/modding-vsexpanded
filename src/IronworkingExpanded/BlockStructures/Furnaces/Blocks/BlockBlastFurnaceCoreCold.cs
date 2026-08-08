using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

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
  /// 160-cell structure map is drawn as nine ASCII cross-sections (one per Y level, y=0 the hearth floor
  /// the core itself sits in, up to y=8 the open stack).</summary>
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
        // on the south, so the anchor reads apart from the hot core and the cupola - and shows which way
        // it faces - at a glance.
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/bfc"
        )
        // The furnace footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X,
        // origin x=-3/z=-2 so the core lands on the layout's own (0,0,0)). y=0 the hearth floor ..
        // y=8 the open top, which is the cold furnace's chimney - it takes no exhaust outlets.
        // Legend: # refractory brick, C the core (origin), I the iron tap, S the slag tap, Y/y the
        // north/south tuyere, H tall hopper, f structure filler, c coal/air, p the crucible floor (same code as c), a air.
        // Compared as an unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            .Legend('#', VanillaCodes.Refractory)
            .Legend(
              'C',
              IwexBlocks.FurnaceBlastcore.WithSide(BlockFacing.NORTH)
            )
            .Legend('I', IwexBlocks.FurnaceIrontap.WithSide(BlockFacing.WEST))
            // The slag tap is its own block. If both glyphs pointed at one code, told apart only by
            // their roles, a player could build the iron notch in the cinder notch's cell and the
            // structure would complete anyway. The two types carry different codes, so the drawing
            // states which hole goes where.
            // Both taps face into the furnace and pour outward, which is the opposite of what the
            // letter reads as: BlockEntityFurnaceTap.TryPourMetal takes
            // `Pos.AddCopy(facing.Opposite).DownCopy()`. So `I` in the EAST wall is declared WEST and
            // pours to (3,0,0), and `S` in the WEST wall is declared EAST and pours to (-3,0,0).
            // And (-3,0,0) is why layer 0's z=0 row opens with `.` rather than `#`. The iron tap's
            // runout lands outside the drawn grid and is free by accident of the footprint; the slag
            // tap's lands inside it, so the cell is deliberately not claimed - a runout notch in the
            // hearth base. Claim it back and the slag tap has nowhere to pour, silently: the structure
            // still completes and the furnace still lights, it just never drains its cinder.
            .Legend('S', IwexBlocks.FurnaceSlagtap.WithSide(BlockFacing.EAST))
            // The blast inlets, orientation-pinned rather than wildcarded, which is why there are two
            // glyphs for what is one block. A tuyere is walled in on three sides, so its cell admits exactly
            // one connector face - north out of the north wall, south out of the south - and a placed node
            // is exchanged onto that face by BlockNetworkNode.RecalculateAndSyncOrientations as soon as the
            // brick goes up. So the drawing can state it, and `MultiblockFacings` rotates the letter with
            // the structure. With a wildcarded legend a tuyere fitted facing into the hearth completes
            // the furnace exactly as a correct one does.
            // The letter is the face the connector sits on, so each tuyere points out at the player's
            // pipe, not in at the hearth: `Y` is drawn in the north row (z=-1) and connects `n`; `T` is
            // in the south row and connects `s`. Getting these the
            // other way round is unbuildable rather than merely odd - a pipe neighbour makes that face
            // required in ComputeValidOrientations, so a placed tuyere is exchanged onto it and the cell
            // demanding the opposite letter can never be satisfied.
            .Legend('Y', IwexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('T', IwexBlocks.FurnaceTuyere.WithOrientation("s"))
            // There is no passthrough legend: the tuyere sits at the wall face at (0,2,±2), so it is the
            // outermost cell and the player's own pipe butts straight against it - a tuyere buried in
            // the brick would need a claimed cell to carry the blast through the wall. The orientation
            // pin holds either way: walled on three sides, the cell admits exactly one connector face -
            // north out of the north wall, south out of the south.
            .Legend('H', IwexBlocks.HopperTall.WithSide(BlockFacing.EAST))
            .Legend('f', ExCodes.Filler)
            // ChargeShaft's domain wildcard is load-bearing, not tidiness - see its own remarks for
            // why a bare alternation would put a lit furnace out the moment the player charged it.
            .Legend('c', IwexCodes.ChargeShaft)
            // The crucible floor: the same code as the shaft above it, drawn with its own glyph only so it
            // can carry a second role. Two glyphs on one code share one block number, so this changes
            // nothing about what the structure requires - see MultiblockLayoutBuilder.Build.
            .Legend('h', IwexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column, named on the drawing rather than re-derived from the legend
            // string. Asking "which cells accept an
            // iwex:furnace-chargepile" answers the same 36 cells only for as long as the shaft
            // glyph keeps `chargepile` in its alternation - a coupling between two files that
            // nothing checks. A role says what the cells are for, so the answer survives the
            // charge block being retyped. `a` is air too and is deliberately not chargeable: it
            // is the vent shaft above the stockline.
            .Role('c', CellRole.Chargeable)
            // The crucible is burden and pool. Charge rests on it while the furnace runs, and the
            // molten iron freezes onto it when the furnace is put out - the two are the same cells
            // today, which is why one glyph carries both roles rather than the drawing splitting them.
            // The layered-charge layout change makes the crucible pool-only; then Chargeable comes off
            // this line and nothing else moves.
            .Role('h', CellRole.Chargeable)
            .Role('h', CellRole.Pool)
            // The blast intake. `Y` is already the tuyere glyph; the role is what lets the furnace ask
            // its own drawing where its tuyeres are instead of carrying a copy of these two offsets.
            .Role('Y', CellRole.Tuyere)
            .Role('T', CellRole.Tuyere)
            // The two drains, named on the drawing rather than as a pair of Vec3i literals in the block
            // entity. Both roles are [SingleCell], so the build refuses a layout that draws either glyph
            // twice - which is what lets the consumer read a point instead of a set.
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
