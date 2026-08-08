using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockNetworkPipe.Blocks;
using SteelmakingExpanded.BlockStructures.SmokeStack.BlockEntities;
using Vintagestory.API.Common;

namespace SteelmakingExpanded.BlockStructures.SmokeStack.Blocks;

/// <summary>
/// Intake/anchor block of the smoke-stack multiblock. Vents surplus exhaust gas from
/// the network to the sky. The build-outline projection (Ctrl + Shift + right-click) is
/// provided by the shared <c>MultiblockStructure</c> block behavior declared in the
/// block JSON.
/// </summary>
[BlockRegister]
public partial class BlockSmokeStackIntake : BlockPipePassthrough, IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The smoke-stack intake blocktype, authored in C# (migrated from smokestack/intake.json). The
  /// anchor of the chimney multiblock: its 72-cell structure map is drawn as one top-down ASCII cross-section
  /// per Y level (y=-1 the refractory base up to the tall y=10 flue), compared as an unordered cell set by
  /// <see cref="DefinitionParity"/>. Its AllowedOrientations + fallback are derived from this def by the base
  /// <see cref="BlockPipe"/> (orientation states [n,s,w,e], fallback "n"), so no hand-written tables.</summary>
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
            .Legend('I', SmexBlocks.SmokestackIntake.Any)
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
        .ShapeByType("*-intake-*-s", "lpex:pipe/outlet")
        .ShapeByType("*-intake-*-e", "lpex:pipe/outlet", rotateY: 90)
        .ShapeByType("*-intake-*-n", "lpex:pipe/outlet", rotateY: 180)
        .ShapeByType("*-intake-*-w", "lpex:pipe/outlet", rotateY: 270)
        .Texture("front1", "game:block/clay/refractory/{refractory}/front1")
        .SideSolid(false)
        .SideOpaque(false),
    ];

  #endregion
}
