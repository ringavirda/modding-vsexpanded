using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The heating (reheat) furnace's core - the same reverberatory arrangement as the puddling furnace, one
/// row deeper and without the tap, because nothing here is ever molten. Stock goes in cold on a two-deep
/// hearth, the firebox flame is drawn across it, and it comes out at rolling heat.
/// <para>
/// It is the furnace the rolling mill demands: stock cools while the player carries it back around for the
/// next pass, so a schedule of any length needs somewhere to put the heat back. Later it also earns a
/// second job - <b>roasting ore</b> before the blast furnace - which is what a reverberatory calciner was
/// for and why the same building serves both (see <c>conventions.md</c> § metal recovery).
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockHeatingFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>
  /// The heating-furnace core blocktype. Seven top-down cross-sections (rows +Z, cols +X). Legend:
  /// <c>#</c> refractory brick, <c>-</c>/<c>i</c> fireclay slab shoulders, <c>K</c> the firebox door,
  /// <c>F</c> the firebox (its own block, two cells), <c>C</c> the core, <c>H</c> the hearth, <c>D</c> the
  /// charge door, <c>f</c> filler, <c>a</c> air - including the ash-pit space under the firebox (the
  /// firebars are part of the firebox block, so no grating cell is drawn there).
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
          // (0,0,0) - see BlockPuddlingFurnaceCore for why -3,-2 would have built the whole furnace
          // offset from the block the player placed.
          // This hearth is a row deeper than the puddling furnace's, so `C` sits at col 6 / row 2, not
          // row 1. The two layouts are otherwise near-identical and the origin is the one line that must
          // not be copied between them: -6,-1 here builds the whole furnace one cell north of the core.
          s.Origin(-6, -2)
            .Legend('#', VanillaCodes.Refractory)
            // The fuel bed - see BlockPuddlingFurnaceCore for why this is a required block rather than
            // an `@(air|coalpile)` legend that lets an empty firebox complete the furnace. Two cells
            // here rather than one: this hearth is a row deeper and its firebox runs the full depth beside
            // it, so the bed costs twice the puddling furnace's for the same visible fill.
            .Legend('F', IwexBlocks.FurnaceFirebox.Any)
            // Slab shoulders round the doorway - see BlockPuddlingFurnaceCore. They open the mouth wide
            // enough to reach all three hearth rows, which is what makes the hearth's flanking filler
            // cells a usable interface. `-up-` never rotates; `-south-` is orientation-checked.
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
            // A fuel bed - see BlockPuddlingFurnaceCore. Two cells here rather than one, because
            // this hearth is a row deeper and its firebox runs the full depth beside it.
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
