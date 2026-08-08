using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The puddling furnace's core - the anchor of a <b>reverberatory</b> megablock, and the first furnace in
/// the suite where the fuel never touches the work. Coke burns in a firebox off to one side; the flame is
/// drawn across a low roof, reverberates down onto the hearth, and leaves up the chimney. That separation is
/// the whole point of the process: it is what lets pig iron be decarburised by an oxidising flame and a
/// fettled bed instead of by being melted in contact with fuel.
/// <para>
/// Consequences that show up in the layout: <b>no tuyeres and no blower</b> - the chimney's natural draft is
/// the air supply, regulated by the cap at the top - and a <b>flue course under the hearth</b> (the grating
/// cells) so the cast-iron bottom plate is cooled from below rather than melting into the charge.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockPuddlingFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>
  /// The puddling-furnace core blocktype. Drawn as eight top-down cross-sections (rows +Z, cols +X, origin
  /// x=-3/z=-2). Legend: <c>#</c> refractory brick, <c>G</c> refractory grating (the under-hearth flue),
  /// <c>-</c> / <c>i</c> the fire-brick slab shoulders round the doorway, <c>K</c> the firebox door,
  /// <c>C</c> the core, <c>H</c> the hearth, <c>D</c> the charge door, <c>M</c> the chimney cap,
  /// <c>f</c> filler, <c>c</c> firebox fuel, <c>a</c> air.
  /// <para>
  /// <b>There is no tap glyph of either kind, and that is correct</b> - puddled iron leaves as pasty
  /// balls through the charge door, so nothing is ever poured out of this furnace. The drawing is the
  /// only statement of where this furnace's drains are, and it says there are none.
  /// </para>
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
        // Orientation marker north, the "PF" type label south - the same read-at-a-glance convention the
        // blast-furnace and cupola cores use.
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
          // Origin is the negation of the core glyph's (col, row) so `C` lands on the anchor's own
          // (0,0,0) - the shipped blast furnace has C at col 3/row 2 with Origin(-3,-2). Here C is at
          // col 6/row 1, so -3,-2 (as drafted) would have built the whole furnace three east and one
          // north of the block the player placed.
          s.Origin(-6, -1)
            .Legend('#', VanillaCodes.Refractory)
            // The firebars are part of `iwex:furnace-firebox`, so no separate grating cell sits under
            // the firebox; its place is left as air - the ash pit that is deliberately not modelled.
            // Slab shoulders round the doorway. They are load-bearing on the gameplay, not the fiction:
            // a half-height course opens the mouth far enough that a player can reach all three hearth
            // rows through it, which is what lets the hearth's flanking filler cells be the interface
            // rather than needing a split mesh and a guessing game about which row a click meant.
            // `-up-` is vertical and never rotates; `-south-` is orientation-checked and turns with the
            // build, so a shoulder laid the wrong way round is refused instead of leaving a visible gap.
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
            // The fuel bed. A legend that admits air means an *empty* firebox satisfies the structure,
            // so the furnace completes with no fuel cell built at all - a required block cannot.
            // Any refractory tier - a firebox is a fuel bed, not a metallurgical shell, and pinning tier
            // 3 would put it out of reach of exactly the early-tier player who needs a puddling hearth
            // first. Its facing is visual only (which way the bars run), so the legend wildcards it.
            .Legend('F', IwexBlocks.FurnaceFirebox.Any)
            .Legend('a', VanillaCodes.Air)
            .Legend('A', VanillaCodes.Air)
            // A fuel bed, not a burden column, and the layout is what says so. The builder refuses a
            // layout claiming both roles.
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
