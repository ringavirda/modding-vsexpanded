using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The beehive coke oven's core - a bank of two sealed chambers sharing one wall, which turn bituminous
/// coal into vanilla <c>game:coke</c> in bulk. It runs vanilla's own coking process scaled up rather than a
/// simulation of its own: the arched crown makes it more efficient and a chamber holds many cells where
/// vanilla cokes one block, so the player gets coke faster and in quantity for the same understanding.
/// See docs/design/machines/coke-oven.md.
/// </summary>
/// <remarks>
/// Fire brick throughout, not refractory: vanilla tags <c>claybricks</c> with
/// <c>cokeOvenViableByType: { "*-fire": true }</c>, so this is the game's own coke-oven masonry, and the
/// oven gates every coke-fired machine after it and must stay cheap. The core carries no <c>tier</c> group
/// for the same reason - there is no tiered brick in the drawing for a tier to match.
/// </remarks>
[BlockRegister]
public partial class BlockCokeOvenCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The coke-oven core blocktype. Four top-down cross-sections (rows +Z, cols +X). Legend: <c>#</c> fire
  /// brick, <c>-</c>/<c>i</c> fire-brick slabs, <c>C</c> the core, <c>c</c> a chamber cell, <c>D</c> a
  /// drawing door, <c>L</c> the crown charging lid, <c>f</c> filler, <c>a</c> the crown void.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(domain, FurnaceCode, "cokeovencore", "furnace/cokeovencore")
        .Class<BlockCokeOvenCore>()
        .EntityClass<BlockEntityCokeOven>()
        .Texture("all", "game:block/clay/brick/four/running/fire1")
        .Texture(
          "north",
          "game:block/clay/brick/four/running/fire1",
          "iiex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/brick/four/running/fire1",
          "iiex:block/furnace/co"
        )
        .MultiblockLayout(s =>
          // Origin is the negation of the core glyph's (col, row), so `C` lands on the anchor's own
          // (0, 0, 0). `C` sits at col 4 / row 2 - the middle of the shared wall, one row back from the
          // door face, which puts the two chambers symmetrically about x = 0.
          s.Origin(-4, -2)
            .Legend('#', VanillaCodes.FireBricks)
            // The chamber cells. Firebox blocks rather than vanilla coal piles: the coal here is the
            // workpiece and nothing is burning, but the behaviour is a fuel-bed abstraction - stacked
            // layers, one substance per cell, its own save - and reusing it gives the oven the branch's
            // charge walk, ignition and away-catch-up for free. Vanilla's own conversion could not have
            // run here in any case: BlockEntityCoalPile.TestCokable demands twelve coke-oven-viable
            // blocks in each pile's own 3x3x3 and a closed game:cokeovendoor beside it, neither of which
            // a bulk chamber can offer.
            .Legend('c', IiexBlocks.FurnaceFirebox.Any)
            // The crown springing: a flat slab course either side of the void, which is the arch this
            // oven's efficiency is attributed to. `-up-` never rotates.
            .Legend('-', VanillaCodes.FireSlab(BlockFacing.UP))
            // Shoulders round the drawing doors, on the same south face, so the mouth is wide enough to
            // reach the chamber behind it. `-south-` is orientation-checked.
            .Legend('i', VanillaCodes.FireSlab(BlockFacing.SOUTH))
            .Legend('C', IiexBlocks.FurnaceCokeovencore.Any)
            // The drawing doors, one per chamber, at the south face of the chamber they open. Deliberately
            // not game:cokeovendoor: vanilla's coking must not fire inside these chambers, because the
            // core owns the bulk cycle.
            .Legend(
              'D',
              IiexBlocks.FurnaceChargedoor.WithSide(BlockFacing.SOUTH)
            )
            // The crown lid, over each chamber's void. Coking is destructive distillation, so the chamber
            // has to close; the lid is the one part of the crown that opens, and it is what the sealed
            // gate reads. A trapdoor cannot serve: its variant groups carry no facing at all, so the
            // orientation check has nothing to verify.
            .Legend(
              'L',
              IiexBlocks.FurnaceChargelid.WithSide(BlockFacing.SOUTH)
            )
            .Legend('f', ExCodes.Filler)
            .Legend('a', VanillaCodes.Air)
            // The chambers, marked so the branch's charge walk finds them. The role reads "fuel heating
            // something else" and here the coal is the work; owner ruling 2026-08-21 kept it anyway rather
            // than mint a near-identical Retort member.
            .Role('c', FurnaceCellRoles.Firebox)
            // No FurnaceCellRoles.Flue anywhere. A sealed retort has no stack, and marking one would hand the
            // oven a natural draught it must not have - StackCourses counts exactly these cells.
            .Layer(
              0,
              """
              # # # # # # # # #
              # # # # # # # # #
              # # # # C # # # #
              # # # # # # # # #
              """
            )
            .Layer(
              1,
              """
              # # # # # # # # #
              # c c c # c c c #
              # c c c # c c c #
              # i D i # i D i #
              """
            )
            .Layer(
              2,
              """
              # # # # # # # # #
              # - a - # - a - #
              # - a - # - a - #
              # # f # # # f # #
              """
            )
            .Layer(
              3,
              """
              . # # # . # # # .
              . # L # . # L # .
              . # # # . # # # .
              . . # . . . # . .
              """
            )
        ),
    ];

  #endregion
}
