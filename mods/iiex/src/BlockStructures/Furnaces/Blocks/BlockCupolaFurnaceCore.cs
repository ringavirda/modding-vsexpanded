using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The cupola furnace's core - the anchor block of the cupola multiblock, at the centre of the lowest
/// layer directly under the shaft, as with the blast-furnace cores. Routes Ctrl + Shift + right-click to
/// the <see cref="BlockEntityCupolaFurnace"/> for the build outline while incomplete. The cupola is
/// charged through the tall hopper (the <c>H</c> cell), re-melts scrap-metal remelt burden into cast iron,
/// and taps metal and slag from the two cells flanking its crucible.
/// </summary>
[BlockRegister]
public partial class BlockCupolaFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The cupola-furnace core blocktype. Narrower than the blast furnaces: a single tuyere, a
  /// single-column shaft, no exhaust outlets (its open top is the stack), and a side-charging tall hopper.
  /// Drawn as seven top-down cross-sections (rows +Z, cols +X, origin x=-1/z=-1 so the core lands on the
  /// layout's own (0,0,0)). Legend: <c>#</c> refractory brick, <c>C</c> the core (origin), <c>I</c> the
  /// metal tap, <c>S</c> the slag tap, <c>T</c> tuyere, <c>H</c> tall hopper, <c>f</c> structure filler,
  /// <c>c</c> shaft charge, <c>h</c> the crucible floor (same code as <c>c</c>), <c>a</c> air. Compared as
  /// an unordered cell set by DefinitionParity, so the w-numbering is free.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(
          domain,
          FurnaceCode,
          "cupolacore",
          "furnace/cupolacore",
          "tier1",
          "tier2",
          "tier3"
        )
        .Class<BlockCupolaFurnaceCore>()
        .EntityClass<BlockEntityCupolaFurnace>()
        // A refractory-brick cube in the tier it was built from ({tier}): plain brick on the side faces,
        // the orientation marker on the north face and the "CF" (cupola furnace) type label on the south.
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/cf"
        )
        .MultiblockLayout(s =>
          s.Origin(-1, -1)
            .Legend('#', VanillaCodes.Refractory)
            .Legend(
              'C',
              IiexBlocks.FurnaceCupolacore.WithSide(BlockFacing.NORTH)
            )
            .Legend('I', IiexBlocks.FurnaceIrontap.WithSide(BlockFacing.EAST))
            // The slag tap is its own block, so the drawing pins which notch goes where rather than
            // leaving both cells open to either.
            .Legend('S', IiexBlocks.FurnaceSlagtap.WithSide(BlockFacing.WEST))
            // The cupola is blown from one wall only, so one glyph and one outward face suffice.
            .Legend('T', IiexBlocks.FurnaceTuyere.Any)
            .Connector('T', BlockFacing.NORTH)
            .Legend('H', IiexBlocks.HopperTall.WithSide(BlockFacing.WEST))
            .Legend('f', ExCodes.Filler)
            .Legend('c', IiexCodes.ChargeShaft)
            // The crucible floor: the shaft's occupants plus the hearthmetal block the furnace stands its
            // bath in. Its own glyph, so it can carry the pool role where the shaft carries the burden.
            .Legend('h', IiexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column: four cells in one column, y=2 up. Cell roles, not legend glyphs, are what
            // callers query for where charge stands.
            .Role('c', FurnaceCellRoles.Chargeable)
            // Pool only, the course below the burden. The cupola's crucible is a single cell, so its whole
            // molten charge stands in one hearthmetal block.
            .Role('h', FurnaceCellRoles.Pool)
            .Role('T', FurnaceCellRoles.Tuyere)
            // The two drains sit on opposite sides of the single crucible cell.
            .Role('I', FurnaceCellRoles.MetalTap)
            .Role('S', FurnaceCellRoles.SlagTap)
            .Layer(
              0,
              """
              # # # #
              # C # .
              # # # #
              """
            )
            .Layer(
              1,
              """
              # # # #
              I h S .
              # # # #
              """
            )
            .Layer(
              2,
              """
              # T # #
              # c # .
              # # # #
              """
            )
            .Layer(
              3,
              """
              # # # .
              # c # .
              # # # .
              """
            )
            .Layer(
              4,
              """
              # # # .
              # c H .
              # # # .
              """
            )
            .Layer(
              5,
              """
              # # # .
              # c f .
              # # # .
              """
            )
            .Layer(
              6,
              """
              . # . .
              # a . .
              . # . .
              """
            )
        ),
    ];

  #endregion
}
