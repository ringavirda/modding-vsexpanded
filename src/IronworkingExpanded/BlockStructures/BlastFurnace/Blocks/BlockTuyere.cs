using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;

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
        .Create(domain, "blastfurnace", "blastfurnace/tuyere")
        .Class<BlockTuyere>()
        .EntityClass("iwex.BlockEntityTuyere")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .Behavior("Lockable")
        .VariantGroup("type", "tuyere")
        .VariantGroup("orientation", "s", "n", "w", "e")
        .ShapeByType("*-tuyere-s", "iwex:blastfurnace/tuyere", rotateY: 0)
        .ShapeByType("*-tuyere-e", "iwex:blastfurnace/tuyere", rotateY: 90)
        .ShapeByType("*-tuyere-n", "iwex:blastfurnace/tuyere", rotateY: 180)
        .ShapeByType("*-tuyere-w", "iwex:blastfurnace/tuyere", rotateY: -90)
        .CreativeCommon("*-tuyere-s")
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
