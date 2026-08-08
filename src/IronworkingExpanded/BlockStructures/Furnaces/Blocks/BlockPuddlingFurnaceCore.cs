using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The puddling furnace's core: the anchor of a reverberatory megablock, where the fuel never touches the
/// work. Coke burns in a firebox off to one side, the flame is drawn across a low roof, reverberates down
/// onto the hearth and leaves up the chimney, so pig iron is decarburised by an oxidising flame and a
/// fettled bed rather than by being melted in contact with fuel.
/// <para>
/// The layout follows from that: no tuyeres and no blower, since the chimney's natural draft is the air
/// supply and the cap at the top regulates it; and a flue course under the hearth, so the cast-iron
/// bottom plate is cooled from below. See <c>docs/design/machines/puddling-furnace.md</c>.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPuddlingFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The puddling-furnace core blocktype. Drawn as eight top-down cross-sections (rows +Z, cols +X).
  /// Legend: <c>#</c> refractory brick, <c>-</c> / <c>i</c> the fire-brick slab shoulders round the
  /// doorway, <c>K</c> the firebox door, <c>C</c> the core, <c>H</c> the hearth, <c>D</c> the charge
  /// door, <c>M</c> the chimney cap, <c>F</c> the firebox, <c>f</c> filler, <c>a</c> air, <c>A</c> the
  /// flue. There is no tap glyph of either kind: puddled iron leaves as pasty balls through the charge
  /// door, so nothing is ever poured out of this furnace.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(
          domain,
          FurnaceCode,
          "puddlingcore",
          "furnace/puddlingcore",
          "tier1",
          "tier2",
          "tier3"
        )
        .Class<BlockPuddlingFurnaceCore>()
        .EntityClass<BlockEntityPuddlingFurnace>()
        // Orientation marker north, the "PF" type label south, matching the blast-furnace and cupola
        // cores.
        .Texture("all", "game:block/clay/refractory/{tier}/front1")
        .Texture(
          "north",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/n"
        )
        .Texture(
          "south",
          "game:block/clay/refractory/{tier}/front1",
          "iwex:block/furnace/pf"
        )
        .MultiblockLayout(s =>
          // Origin is the negation of the core glyph's (col, row), so `C` lands on the anchor's own
          // (0,0,0). Here `C` is at col 6, row 1.
          s.Origin(-6, -1)
            .Legend('#', VanillaCodes.Refractory)
            // The firebars are part of `iwex:furnace-firebox`, so no separate grating cell sits under
            // the firebox; its place is left as air, and the ash pit is not modelled.
            // Slab shoulders round the doorway: the half-height course opens the mouth far enough that
            // a player can reach all three hearth rows through it, which is what lets the hearth's
            // flanking filler cells be the interface instead of a split mesh. `-up-` is vertical and
            // never rotates; `-south-` is orientation-checked and turns with the build, so a shoulder
            // laid the wrong way round is refused rather than leaving a visible gap.
            .Legend('-', VanillaCodes.FireSlab(BlockFacing.UP))
            .Legend('i', VanillaCodes.FireSlab(BlockFacing.SOUTH))
            .Legend('K', VanillaCodes.Sealing(BlockFacing.SOUTH))
            .Legend('C', IwexBlocks.FurnacePuddlingcore.Any)
            .Legend(
              'H',
              IwexBlocks.FurnacePuddlinghearth.WithSide(BlockFacing.NORTH)
            )
            .Legend(
              'D',
              IwexBlocks.FurnacePuddlingchargedoor.WithSide(BlockFacing.SOUTH)
            )
            .Legend(
              'M',
              IwexBlocks.FurnacePuddlingchimneycap.WithSide(BlockFacing.NORTH)
            )
            .Legend('f', ExCodes.Filler)
            // The fuel bed. Any refractory tier, and its facing is visual only (which way the bars
            // run), so the legend wildcards the variant.
            .Legend('F', IwexBlocks.FurnaceFirebox.Any)
            .Legend('a', VanillaCodes.Air)
            .Legend('A', VanillaCodes.Air)
            // A fuel bed, not a burden column. The builder refuses a layout claiming both roles.
            .Role('F', CellRole.Firebox)
            .Role('A', CellRole.Flue)
            .Layer(
              0,
              """
              # # # - - - # .
              - a # f H f C #
              # # # - - - # .
              """
            )
            .Layer(
              1,
              """
              # # # # # # # .
              # F # f f f a #
              # K # i D i # .
              """
            )
            .Layer(
              2,
              """
              # # # # # # # .
              # a a - # # a #
              # # # # f # # .
              """
            )
            .Layer(
              3,
              """
              . . . . . . # .
              . # # # # # A #
              . . . . . . # .
              """
            )
            .Layer(
              4,
              """
              . . . . . . # .
              . . . . . # A #
              . . . . . . # .
              """
            )
            .Layer(
              5,
              """
              . . . . . . # .
              . . . . . # A #
              . . . . . . # .
              """
            )
            .Layer(
              6,
              """
              . . . . . . # .
              . . . . . # A #
              . . . . . . # .
              """
            )
            .Layer(
              7,
              """
              . . . . . . . .
              . . . . . M . .
              . . . . . . . .
              """
            )
        ),
    ];

  #endregion
}
