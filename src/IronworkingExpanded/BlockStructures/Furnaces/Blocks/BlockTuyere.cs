using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// Tuyere: a single-faced gas-pipe node built into the blast furnace through which
/// air or hot blast is drawn into the hearth.
/// </summary>
[BlockRegister]
public partial class BlockTuyere : BlockPipe, IExBlockDefProvider
{
  /// <summary>The tuyere blocktype, authored in C# (migrated from blastfurnace/tuyere.json). Its
  /// AllowedOrientations + GetFallbackOrientation are derived from this def by the <see cref="BlockPipe"/>
  /// base (variant order [s,n,w,e], fallback "s" = its first-listed state), so no hand-written tables.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "tuyere", "furnaces/tuyere")
        .Class<BlockTuyere>()
        .EntityClass("iwex.BlockEntityTuyere")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .Behavior("Lockable")
        .VariantGroup("type", "tuyere")
        .VariantGroup("orientation", "s", "n", "w", "e")
        .ShapeByType("*-s", "iwex:furnaces/tuyere", rotateY: 0)
        .ShapeByType("*-e", "iwex:furnaces/tuyere", rotateY: 90)
        .ShapeByType("*-n", "iwex:furnaces/tuyere", rotateY: 180)
        .ShapeByType("*-w", "iwex:furnaces/tuyere", rotateY: -90)
        .CreativeCommon("*-s")
        .Replaceable(400)
        .Resistance(3.5f)
        .LightAbsorption(3)
        .Sound("walk", "walk/stone")
        .Sound("place", "block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        )
        .SolidNonOpaque(),
    ];
}
