using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The cupola furnace's core - the anchor block of the cupola multiblock, sitting at the centre of the
/// lowest layer directly under the shaft, exactly like the blast-furnace cores. Routes Ctrl + Shift +
/// right-click to the <see cref="BlockEntityCupolaFurnace"/> for the build outline while incomplete.
/// <para>
/// The cupola replaces the retired charging-hatch anchor: the hatch parked the structure part-way up the
/// shaft, the core anchors on the ground the same way the blast furnaces now do. The cupola is charged
/// through the tall hopper (the <c>H</c> cell), re-melts scrap-metal remelt burden into cast iron, and
/// taps cast iron out its lower tap and slag out its upper.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockCupolaFurnaceCore
  : BlockFurnaceCoreBase,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The cupola-furnace core blocktype. A narrower furnace than the blast furnaces: a single
  /// tuyere, a single-column shaft, no exhaust outlets (its open top is the stack), and a side-charging
  /// tall hopper. Drawn as seven top-down cross-sections (rows +Z, cols +X, origin x=-1/z=-1 so the core
  /// lands on the layout's own (0,0,0)). Legend: # refractory brick, C the core (origin), T the metal
  /// tap, S the slag tap (same code as T), Y tuyere, H tall hopper, f structure filler, c coal/air,
  /// p the crucible floor (same code as c), a air. Compared as an unordered cell set by
  /// DefinitionParity, so the w-numbering is free.</summary>
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
        // A refractory-brick cube in whichever tier it was built from ({tier}): plain brick on the side
        // faces, the orientation marker on the north face and the "CF" (cupola furnace) type label on the
        // south, so a built cupola reads apart from the blast-furnace cores - and shows which way it faces
        // - at a glance.
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
            // The slag tap - its own block since the taps were typed, so the drawing states which notch
            // goes where instead of leaving both cells open to either. See the cold furnace.
            .Legend('S', IwexBlocks.FurnaceSlagtap.WithSide(BlockFacing.WEST))
            // Orientation-pinned - see the cold furnace. The cupola is blown from one wall only, so
            // it needs just the north letter.
            .Legend('T', IwexBlocks.FurnaceTuyere.WithOrientation("n"))
            .Legend('H', IwexBlocks.HopperTall.WithSide(BlockFacing.WEST))
            .Legend('f', ExCodes.Filler)
            .Legend('c', IwexCodes.ChargeShaft)
            // The crucible floor - the same code as the shaft, its own glyph only so it can carry the
            // pool role beside the burden one. See the cold furnace.
            .Legend('h', IwexCodes.HearthCell)
            .Legend('a', VanillaCodes.Air)
            // The burden column - see the cold furnace for why the role rather than the legend
            // string answers "where does charge stand". Five cells, one column: the cupola is the
            // only furnace whose shaft box is its charge volume rather than merely containing it.
            .Role('c', CellRole.Chargeable)
            // Burden and pool at once, and the narrowest case of it: the cupola's crucible is a single
            // cell, so its whole molten charge freezes into one block when it is put out.
            .Role('h', CellRole.Chargeable)
            .Role('h', CellRole.Pool)
            // One tuyere, not two - the cupola is the narrow furnace. Asked off the drawing now.
            .Role('T', CellRole.Tuyere)
            // The two drains. The cupola's are on opposite sides and a course apart - cast iron out low
            // to the west, slag off the top of the bath to the east - which the drawing now states.
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
