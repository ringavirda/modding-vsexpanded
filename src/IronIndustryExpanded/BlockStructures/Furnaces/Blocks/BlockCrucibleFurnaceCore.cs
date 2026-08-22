using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The crucible furnace's core (Huntsman, 1740) - a deep hole with four sealed pots standing in a coke
/// fire, an ash pit under the grate and a stack over it. Nothing here is blown: the whole machine is
/// natural draught, and how hot it gets is how tall the player built the chimney.
/// </summary>
[BlockRegister]
public partial class BlockCrucibleFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The crucible-furnace core blocktype. Five top-down cross-sections (rows +Z, cols +X), from the ash
  /// pit at layer -1 up. Legend: <c>#</c> refractory brick, <c>-</c> fire-brick slab shoulders, <c>K</c>
  /// the ash-pit door, <c>C</c> the core, <c>H</c> the hearth, <c>D</c> the charge door, <c>M</c> the
  /// damper, <c>b</c> any brick family, <c>a</c> air, <c>A</c> air carrying the flue role, <c>f</c>
  /// filler.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(
          domain,
          FurnaceCode,
          "cruciblecore",
          "furnace/cruciblecore",
          "tier1",
          "tier2",
          "tier3"
        )
        .Class<BlockCrucibleFurnaceCore>()
        .EntityClass<BlockEntityCrucibleFurnace>()
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iiex:block/furnace/cs"
        )
        .MultiblockLayout(s =>
          // Origin is the negation of the core glyph's (col, row) so `C` lands on the anchor's own
          // (0,0,0). Here `C` is at col 3, row 2, and the furnace is a single column: ash pit, hearth,
          // damper, flue, then the first course of the player's chimney.
          s.Origin(-3, -2)
            .Legend('#', VanillaCodes.Refractory)
            // The outer stack is the player's own masonry, not refractory: the course is the same
            // brick-family ring siex's smokestack draws, so one predicate serves every stack in the suite.
            .Legend('b', VanillaCodes.AnyBricks)
            // Slab shoulders round the mouth, as both reverberatory furnaces have: they open the front far
            // enough that a player can reach into the hole with tongs. `-up-` never rotates.
            .Legend('-', VanillaCodes.FireSlab(BlockFacing.UP))
            // The ash-pit door. Air enters under the grate here, and the ash is raked out of it.
            .Legend('K', VanillaCodes.Sealing(BlockFacing.SOUTH))
            .Legend('C', IiexBlocks.FurnaceCruciblecore.Any)
            .Legend('H', IiexBlocks.FurnaceCruciblehearth.Any)
            .Legend(
              'D',
              IiexBlocks.FurnaceChargedoor.WithSide(BlockFacing.SOUTH)
            )
            // The damper, and it is the puddling furnace's chimney cap: the same mechanic at the other end
            // of the flue, so one implementation serves both. Faced south so its housing filler lands to
            // the north, behind the flue rather than in it.
            .Legend(
              'M',
              IiexBlocks.FurnacePuddlingchimneycap.WithSide(BlockFacing.SOUTH)
            )
            .Legend('f', ExCodes.Filler)
            .Legend('a', VanillaCodes.Air)
            .Legend('A', VanillaCodes.Air)
            // The fire and the work are the same cell: the coke is packed round the pots rather than
            // burning beside them. One firebox cell, so the hearth lights on one cell's worth of fuel.
            .Role('H', CellRole.Firebox)
            .Role('A', CellRole.Flue)
            // The first production use of the role. The crucible furnace's damper sits at the foot of the
            // stack rather than its top, which is the only place a layout can draw one when the chimney
            // above it is the player's to build.
            .Role('M', CellRole.Damper)
            .Layer(
              -1,
              """
              . . . .
              # # # .
              # a # .
              # K # .
              """
            )
            .Layer(
              0,
              """
              . . . .
              # # # .
              # H # C
              - D - .
              """
            )
            .Layer(
              1,
              """
              . . . .
              . f . .
              # M # .
              . # . .
              """
            )
            .Layer(
              2,
              """
              . . . .
              . # . .
              # A # .
              . # . .
              """
            )
            // The base course of the chimney, and the only one the layout draws. The rest is the player's:
            // height buys temperature, and the core counts what was actually built rather than the drawing
            // declaring it. `. b . / b a b / . b .` is the same ring siex's smokestack validates.
            .Layer(
              3,
              """
              . . . .
              . b . .
              b A b .
              . b . .
              """
            )
        ),
    ];

  #endregion
}
