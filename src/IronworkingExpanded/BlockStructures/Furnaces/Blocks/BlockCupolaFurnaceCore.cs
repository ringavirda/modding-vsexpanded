using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

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
  /// lands on the layout's own (0,0,0)). Legend: # refractory brick, C the core (origin), T tap,
  /// Y tuyere, H tall hopper, f structure filler, c coal/air, a air. Compared as an unordered cell set
  /// by DefinitionParity, so the w-numbering is free.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Core(domain, "cupolafurnacecore", "furnaces/cupola-core")
        .Class<BlockCupolaFurnaceCore>()
        .EntityClass<BlockEntityCupolaFurnace>()
        // Any refractory tier - the cupola runs cooler than the hot blast furnace. Tier-2 face, so a
        // built cupola reads apart from a tier-1 cold blast furnace at a glance.
        .Texture("all", "game:block/clay/refractory/tier2/front1")
        .MultiblockLayout(s =>
          s.Origin(-1, -1)
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('C', "iwex:cupolafurnacecore-*")
            .Legend('T', "iwex:moltenmetaltap*")
            .Legend('Y', "iwex:tuyere*")
            .Legend('H', "iwex:hopper-tall*")
            .Legend('f', "exlib:structurefiller")
            .Legend('c', "@(air|coalpile)")
            .Legend('a', "game:air")
            .Layer(
              0,
              """
              # # #
              # C #
              # # #
              """
            )
            .Layer(
              1,
              """
              # Y #
              T c #
              # # #
              """
            )
            .Layer(
              2,
              """
              # # #
              # c T
              # # #
              """
            )
            .Layer(
              3,
              """
              # # #
              # c #
              # # #
              """
            )
            .Layer(
              4,
              """
              # # #
              # c H
              # # #
              """
            )
            .Layer(
              5,
              """
              # # #
              # c f
              # # #
              """
            )
            .Layer(
              6,
              """
              . # .
              # a .
              . # .
              """
            )
        ),
    ];

  #endregion
}
