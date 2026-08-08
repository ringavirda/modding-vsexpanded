using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

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
          "iwex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/cf"
        )
        .MultiblockLayout(s =>
          s.Origin(-1, -1)
            .Legend('#', VanillaCodes.Refractory)
            .Legend(
              'C',
              IwexBlocks.FurnaceCupolacore.WithSide(BlockFacing.NORTH)
            )
            .Legend('I', IwexBlocks.FurnaceIrontap.WithSide(BlockFacing.EAST))
            // The slag tap is its own block, so the drawing pins which notch goes where rather than
            // leaving both cells open to either.
            .Legend('S', IwexBlocks.FurnaceSlagtap.WithSide(BlockFacing.WEST))
            // Orientation-pinned. The cupola is blown from one wall only, so one letter suffices.
            .Legend('T', IwexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('H', IwexBlocks.HopperTall.WithSide(BlockFacing.WEST))
            .Legend('f', ExCodes.Filler)
            .Legend('c', IwexCodes.ChargeShaft)
            // The crucible floor. Same code as the shaft; it has its own glyph so it can carry the pool
            // role alongside the burden one.
            .Legend('h', IwexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column: five cells in one column. Cell roles, not legend glyphs, are what
            // callers query for where charge stands.
            .Role('c', CellRole.Chargeable)
            // Burden and pool at once. The cupola's crucible is a single cell, so its whole molten charge
            // freezes into one block when it is put out.
            .Role('h', CellRole.Chargeable)
            .Role('h', CellRole.Pool)
            .Role('T', CellRole.Tuyere)
            // The two drains sit on opposite sides of the single crucible cell.
            .Role('I', CellRole.MetalTap)
            .Role('S', CellRole.SlagTap)
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
