using System.Collections.Generic;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// Tuyere: a single-faced gas-pipe node built into the blast furnace through which
/// air or hot blast is drawn into the hearth.
/// </summary>
[BlockRegister]
public partial class BlockTuyere : BlockPipe, IExBlockDefProvider {
  /// <summary>The tuyere blocktype. AllowedOrientations and GetFallbackOrientation are derived from this
  /// def by the <see cref="BlockPipe"/> base: variant order [s,n,w,e], fallback "s" as the first-listed
  /// state.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/tuyere")
        .Class<BlockTuyere>()
        .EntityClass("iiex.BlockEntityTuyere")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        // Build-outline projection: the tuyere is a functional cell of the furnace layout, so a player at
        // it can preview and complete an incomplete furnace. Must precede other rmb consumers (Lockable).
        .Behavior("MultiblockStructure")
        .Behavior("Lockable")
        .VariantGroup("type", "tuyere")
        .VariantGroup("orientation", "s", "n", "w", "e")
        .NetworkOriented()
        .ShapeByType("*-s", "iiex:furnace/tuyere", rotateY: 0)
        .ShapeByType("*-e", "iiex:furnace/tuyere", rotateY: 90)
        .ShapeByType("*-n", "iiex:furnace/tuyere", rotateY: 180)
        .ShapeByType("*-w", "iiex:furnace/tuyere", rotateY: -90)
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
