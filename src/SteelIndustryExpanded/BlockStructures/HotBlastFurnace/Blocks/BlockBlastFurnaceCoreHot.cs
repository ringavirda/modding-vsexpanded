using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks;

/// <summary>
/// Anchor block of the hot blast furnace multiblock, at the centre of the lowest layer directly under the
/// hearth column. Routes Ctrl + Shift + right-click to the <see cref="BlockEntityBlastFurnaceHot"/> for
/// the build outline while the structure is incomplete.
/// </summary>
[BlockRegister]
public partial class BlockBlastFurnaceCoreHot
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The hot blast-furnace core blocktype. Same shape and anchor rules as the cold core, but
  /// tier-3 refractory only, and its shaft carries the exhaust outlets and the bell/reinforced hoppers the
  /// cold furnace does without. See docs/design/machines/blast-furnace-hot.md.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(domain, "blastfurnacecore", type: null, "blastfurnace/core")
        .Class<BlockBlastFurnaceCoreHot>()
        .EntityClass<BlockEntityBlastFurnaceHot>()
        // A tier-3 refractory-brick cube: plain brick on the side faces, the orientation marker on the
        // north face and the "BF/H" (blast furnace, hot) type label on the south, so the anchor reads
        // apart from the cold core and the cupola and shows which way it faces.
        .Texture("all", "game:block/clay/refractory/tier3/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/tier3/front1",
          "siex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/tier3/front1",
          "siex:block/furnace/bfh"
        )
        // The furnace footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X,
        // origin x=-3/z=-2 so the core lands on the layout's own (0,0,0)). y=0 the hearth floor ..
        // y=8 the reinforced charging hopper. Legend: # refractory brick, C the core (origin), T the
        // iron tap, S the slag tap, Y/y the north/south tuyere, P pipe outlet, B bell hopper,
        // R reinforced hopper, c coal/air, p the crucible floor (same code as c), a air. Compared as an
        // unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, -2)
            // Tier 3 exactly, not VanillaCodes.Refractory: the hot blast admits no lower tier, and this
            // is the one shell in the family that pins its material.
            .Legend('#', VanillaCodes.RefractoryTier(3))
            .Legend('C', SiexBlocks.BlastfurnaceCore.Any)
            // Facing-pinned rather than `.Any`, as the tuyeres below are: a wildcarded facing lets a tap
            // be fitted the wrong way round and still complete the structure, giving a furnace that
            // lights and melts but never drains.
            // The facing reads inverted. `BlockEntityFurnaceTap.TryPourMetal` spouts at
            // `Pos.AddCopy(facing.Opposite).DownCopy()`, so a tap faces into the furnace and drains out
            // the other way. `T` sits in the east wall at (2,1,0) declared west, so it pours to (3,0,0),
            // outside the drawn grid.
            .Legend('T', IiexBlocks.FurnaceIrontap.WithSide(BlockFacing.WEST))
            // The slag tap is its own block, so this drawing states which notch goes where. `S` sits in
            // the west wall at (-2,2,0) declared east, so it pours to (-3,1,0) - the `.` at the west end
            // of layer 1's z=0 row. That cell is its runout: claim it back and the cinder notch has
            // nowhere to drain, while the structure still completes and the furnace still lights.
            .Legend('S', IiexBlocks.FurnaceSlagtap.WithSide(BlockFacing.EAST))
            // The blast inlets, orientation-pinned rather than wildcarded, which is why one block takes
            // two glyphs. A tuyere is walled in on three sides, so its cell admits exactly one connector
            // face - north out of the north wall, south out of the south - and
            // BlockNetworkNode.RecalculateAndSyncOrientations exchanges a placed node onto that face as
            // soon as the brick goes up; `MultiblockFacings` rotates the letter with the structure. A
            // wildcarded legend (`iiex:furnace-tuyere-*`) would let a tuyere fitted facing into the hearth
            // complete the furnace.
            .Legend('Y', IiexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('y', IiexBlocks.FurnaceTuyere.WithOrientation("s"))
            .Legend('P', IiexCodes.PipeOutlet)
            .Legend('R', SiexBlocks.BlastfurnaceHopperreinforced.Code)
            .Legend('B', SiexBlocks.BlastfurnaceHopperbell.Code)
            .Legend('c', IiexCodes.ChargeShaft)
            // The crucible floor, its own glyph so it can carry the pool role beside the burden one.
            // `HearthCell`, not `ChargeShaft`: the two differ by the hearth-metal code, and the pool
            // stands on this course, so a cell that does not admit `iiex:hearthmetal-*` is rejected by
            // the structure the moment metal appears in it.
            .Legend('p', IiexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column. The role, not the legend string, answers where charge stands, and this
            // drawing states it rather than inheriting it. The cells differ from the cold furnace on
            // purpose: that one's crucible spans the full hearth course (39 cells) with its cinder notch
            // level with the iron one, while this keeps a narrow two-cell crucible and a high slag tap.
            .Role('c', CellRole.Chargeable)
            // Burden and pool at once, as the cold furnace's crucible is.
            .Role('p', CellRole.Chargeable)
            .Role('p', CellRole.Pool)
            .Role('Y', CellRole.Tuyere)
            .Role('y', CellRole.Tuyere)
            // The only furnace of the three that vents; the cold furnace and the cupola declare no
            // outlet cells at all.
            .Role('P', CellRole.GasOutlet)
            // The two drains. The iron notch matches the cold furnace's cell, the cinder notch does not -
            // see the burden-column note above.
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
