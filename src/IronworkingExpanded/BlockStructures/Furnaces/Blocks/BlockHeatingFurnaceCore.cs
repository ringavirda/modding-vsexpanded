using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The heating (reheat) furnace's core - the same reverberatory arrangement as the puddling furnace, one
/// row deeper and without a tap, since nothing here is ever molten. Stock goes in cold on a two-deep
/// hearth, the firebox flame is drawn across it, and it comes out at rolling heat. The same building is
/// also intended to roast ore before the blast furnace; see docs/design/conventions.md, metal recovery.
/// </summary>
[BlockRegister]
public partial class BlockHeatingFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The heating-furnace core blocktype. Five top-down cross-sections (rows +Z, cols +X). Legend:
  /// <c>#</c> refractory brick, <c>-</c>/<c>i</c> fireclay slab shoulders, <c>K</c> the firebox door,
  /// <c>F</c> the firebox (its own block, two cells), <c>C</c> the core, <c>H</c> the hearth, <c>D</c> the
  /// charge door, <c>f</c> filler, <c>a</c> air (including the ash-pit space under the firebox; the
  /// firebars belong to the firebox block, so no grating cell is drawn there), <c>A</c> air carrying the
  /// flue role.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(
          domain,
          FurnaceCode,
          "heatingcore",
          "furnace/heatingcore",
          "tier1",
          "tier2",
          "tier3"
        )
        .Class<BlockHeatingFurnaceCore>()
        .EntityClass<BlockEntityHeatingFurnace>()
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/hf"
        )
        .MultiblockLayout(s =>
          // Origin is the negation of the core glyph's (col, row) so `C` lands on the anchor's own
          // (0,0,0). This hearth is a row deeper than the puddling furnace's, so `C` sits at col 6 /
          // row 2. The two layouts are otherwise near-identical; the origin must not be copied between
          // them, as -6,-1 here would build the whole furnace one cell north of the core.
          s.Origin(-6, -2)
            .Legend('#', VanillaCodes.Refractory)
            // The fuel bed, a required block rather than an `@(air|coalpile)` legend, so an empty firebox
            // cannot complete the furnace. Two cells here: the hearth is a row deeper and the firebox
            // runs the full depth beside it.
            .Legend('F', IwexBlocks.FurnaceFirebox.Any)
            // Slab shoulders round the doorway. They open the mouth wide enough to reach all three hearth
            // rows, which is what makes the hearth's flanking filler cells a usable interface. `-up-`
            // never rotates; `-south-` is orientation-checked.
            .Legend('-', VanillaCodes.FireSlab(BlockFacing.UP))
            .Legend('i', VanillaCodes.FireSlab(BlockFacing.SOUTH))
            .Legend('K', VanillaCodes.Sealing(BlockFacing.SOUTH))
            .Legend('C', IwexBlocks.FurnaceHeatingcore.Any)
            .Legend(
              'D',
              IwexBlocks.FurnaceChargedoor.WithSide(BlockFacing.SOUTH)
            )
            .Legend(
              'H',
              IwexBlocks.FurnaceHeatinghearth.WithSide(BlockFacing.NORTH)
            )
            .Legend('f', ExCodes.Filler)
            .Legend('a', VanillaCodes.Air)
            .Legend('A', VanillaCodes.Air)
            .Role('F', CellRole.Firebox)
            .Role('A', CellRole.Flue)
            .Layer(
              0,
              """
              # # # - - - # .
              a a # f f f # #
              a a # f H f C #
              # # # - - - # .
              """
            )
            .Layer(
              1,
              """
              # # # # # # # .
              # F # f f f a #
              # F # f f f a #
              # K # i D i # .
              """
            )
            .Layer(
              2,
              """
              # # # # # # # .
              # a a - # # # #
              # a a - # # a #
              # # # # f # # .
              """
            )
            .Layer(
              3,
              """
              . . . . . . . .
              . # # # # # # .
              . # # # # # A #
              . . . . . . # .
              """
            )
            .Layer(
              4,
              """
              . . . . . . . .
              . . . . . . # .
              . . . . . # A #
              . . . . . . # .
              """
            )
        ),
    ];

  #endregion
}
