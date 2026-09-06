using System.Collections.Generic;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using SteelIndustryExpanded.BlockStructures.SmokeStack.BlockEntities;
using Vintagestory.API.Common;

namespace SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks;

/// <summary>
/// Intake and anchor block of the smoke-stack multiblock; vents surplus exhaust gas from the network to
/// the sky. The build-outline projection (Ctrl + Shift + right-click) comes from the shared
/// <c>MultiblockStructure</c> block behavior declared in the definition below.
/// </summary>
[BlockRegister]
public partial class BlockSmokeStackIntake
  : BlockPipePassthrough,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>The smoke-stack intake blocktype, anchor of the 72-cell chimney multiblock. The base
  /// <see cref="BlockPipe"/> derives AllowedOrientations and the fallback from this definition
  /// (orientation states n, s, w, e with fallback "n"), so neither is written out by hand.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "smokestack", "smokestack/intake")
        .Class<BlockSmokeStackIntake>()
        .EntityClass<BlockEntitySmokeStack>()
        .Material(EnumBlockMaterial.Ceramic)
        .Sound("walk", "game:walk/stone")
        .Sound("place", "game:block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "game:block/rock-hit-pickaxe",
          "game:block/rock-break-pickaxe"
        )
        .MaxStackSize(1)
        .Handbook("smokestack-intake-*")
        // The chimney footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X, origin
        // x=-1/z=0). y=-1 refractory base .. y=10 the flue mouth. Legend: # refractory brick, I the intake
        // (origin), a air, B chimney brick-course. Compared as an unordered cell set by DefinitionParity.
        .MultiblockLayout(s =>
          s.Origin(-1, 0)
            .Legend('#', VanillaCodes.Refractory)
            .Legend('I', SiexBlocks.SmokestackIntake.Any)
            .Legend('a', VanillaCodes.Air)
            .Legend('B', VanillaCodes.AnyBricks)
            .Layer(
              -1,
              """
              # # #
              # # #
              # # #
              """
            )
            .Layer(
              0,
              """
              # I #
              # a #
              # # #
              """
            )
            .Layer(
              1,
              """
              # # #
              # a #
              # # #
              """
            )
            .Layer(
              2,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              3,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              4,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              5,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              6,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              7,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              8,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              9,
              """
              . B .
              B a B
              . B .
              """
            )
            .Layer(
              10,
              """
              . B .
              B a B
              . B .
              """
            )
        )
        .CreativeCommon("*-intake-*-n")
        .Behavior("MultiblockStructure")
        .Behavior("Lockable")
        .VariantGroup("type", "intake")
        .VariantGroup("refractory", "tier1", "tier2", "tier3")
        .VariantGroup("orientation", "n", "s", "w", "e")
        .NetworkOriented()
        .ShapeByType("*-intake-*-s", "iiex:pipe/outlet")
        .ShapeByType("*-intake-*-e", "iiex:pipe/outlet", rotateY: 90)
        .ShapeByType("*-intake-*-n", "iiex:pipe/outlet", rotateY: 180)
        .ShapeByType("*-intake-*-w", "iiex:pipe/outlet", rotateY: 270)
        .Texture("front1", "game:block/clay/refractory/{refractory}/front1")
        .SideSolid(false)
        .SideOpaque(false),
    ];

  #endregion
}
