using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The cupola's charging hatch. Still the anchor of the cupola multiblock, but only until the cupola
/// gains its own bottom-centre core (the same move the blast furnaces just made); it then becomes a
/// pure loading hatch and loses the layout below.
/// <para>
/// PUBLISHED CUPOLA FRAME - the coordinate contract later stages are written against. When
/// <c>BlockCupolaCore</c> + <c>BlockEntityCupola</c> land, the layout below moves onto the core with
/// <c>Origin(-1, -1)</c> and layers renumbered <c>0..7</c>; the core takes the bottom-layer centre,
/// which is this frame's <c>(1, -4, 0)</c>. Every offset therefore translates
/// <c>core = hatch - (1, -4, 0)</c>, giving: core (0,0,0), tuyere (0,1,-1), cast-iron tap (-1,1,0),
/// slag tap (1,2,0), shaft column (0,1..5,0), shaft centre (0,3,0), this hatch (-1,4,0), its filler
/// (-1,5,0), open stack (0,6,0)+(0,7,0), and no gas outlets.
/// <see cref="IronworkingExpanded.Tests"/>' furnace-offset test pins that table.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockCupolaInput : BlockPipe, IExBlockDefProvider
{
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "cupola-input", "furnaces/cupola-input")
        .Class<BlockCupolaInput>()
        // NOTE: no BlockEntityHopperTall type exists in the repo - this (and BlockHopperTall's
        // identical binding) is a dangling string binding, left alone here because fixing it means
        // authoring the missing block entity, which belongs to the hopper's owner, not this change.
        .EntityClass("iwex.BlockEntityHopperTall")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 1)
              .Slice(
                0,
                """
                #
                0
                """
              )
          )
        )
        // Rows +Z, cols +X. Origin(0, -1) puts the hatch 'I' on the layout's own (0,0,0): the
        // previous Origin(-3, 0) was copy-pasted from the six-wide furnace door onto these
        // three-wide grids, which parked the anchor at (-3,0,1) and left the cupola unbuildable.
        .MultiblockLayout(s =>
          s.Origin(0, -1)
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('f', "exlib:structurefiller")
            .Legend('I', "iwex:cupola-input*")
            .Legend('T', "iwex:moltenmetaltap*")
            .Legend('Y', "iwex:tuyere*")
            .Legend('c', "@(air|coalpile)")
            .Legend('a', "game:air")
            .Layer(
              -4,
              """
              # # #
              # # #
              # # #
              """
            )
            .Layer(
              -3,
              """
              # Y #
              T c #
              # # #
              """
            )
            .Layer(
              -2,
              """
              # # #
              # c T
              # # #
              """
            )
            .Layer(
              -1,
              """
              # # #
              # c #
              # # #
              """
            )
            .Layer(
              0,
              """
              # # #
              I c #
              # # #
              """
            )
            .Layer(
              1,
              """
              # # #
              f c #
              # # #
              """
            )
            .Layer(
              2,
              """
              # # #
              # a #
              # # #
              """
            )
            .Layer(
              3,
              """
              . # .
              # a #
              . # .
              """
            )
        )
        .SolidNonOpaque(),
    ];
}
