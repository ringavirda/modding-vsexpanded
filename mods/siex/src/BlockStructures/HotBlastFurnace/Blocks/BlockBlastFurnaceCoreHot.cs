using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
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
            // The blast inlets. One block takes two glyphs because the two cells demand opposite faces:
            // north out of the north wall, south out of the south. The demand is on the connector rather
            // than on the code - a network node re-picks its orientation from its neighbours, so a pinned
            // variant states a fact the node is free to contradict, and a bare wildcard would let a tuyere
            // fitted facing into the hearth complete the furnace.
            .Legend('Y', IiexBlocks.FurnaceTuyere.Any)
            .Legend('y', IiexBlocks.FurnaceTuyere.Any)
            .Connector('Y', BlockFacing.NORTH)
            .Connector('y', BlockFacing.SOUTH)
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
            // purpose: that one's crucible spans the full hearth course with its cinder notch level with
            // the iron one, while this keeps a narrow two-cell crucible and a high slag tap.
            .Role('c', FurnaceCellRoles.Chargeable)
            // Burden and pool at once, which is known-wrong and held deliberately. The cold furnace and
            // the cupola separated the two when their crucible became live molten cells; this drawing
            // cannot follow by dropping `Chargeable` alone, because that leaves a two-cell crucible under
            // a 3x3 shaft - a bosh no furnace has - and bakes it into a golden. The crucible, the cinder
            // notch a course too high and the tuyeres move together in the smex remake, one layout change
            // and one blessing. What this costs meanwhile is pinned by CrucibleOverlapTests: two of the
            // nine columns draw a block short for a campaign, and the units they hide still count.
            .Role('p', FurnaceCellRoles.Chargeable)
            .Role('p', FurnaceCellRoles.Pool)
            .Role('Y', FurnaceCellRoles.Tuyere)
            .Role('y', FurnaceCellRoles.Tuyere)
            // The only furnace of the three that vents; the cold furnace and the cupola declare no
            // outlet cells at all.
            .Role('P', FurnaceCellRoles.GasOutlet)
            // The two drains. The iron notch matches the cold furnace's cell, the cinder notch does not -
            // see the burden-column note above.
            //
            // Since the drawn tap shapes landed, that costs a second known-wrong thing, deferred with the
            // crucible: each shape encodes its own notch height (iron channel at model Y 2-3, cinder at
            // Y 10-11), so the course between the two notches is art and both taps belong at y=1. `S`
            // sits at y=2 here, so this furnace draws its cinder notch a course higher than the metal it
            // skims - the height is counted twice. Moving `S` down is a layout change and a blessing, and
            // it belongs in the same remake that separates Pool from Chargeable.
            .Role('T', FurnaceCellRoles.MetalTap)
            .Role('S', FurnaceCellRoles.SlagTap)
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
