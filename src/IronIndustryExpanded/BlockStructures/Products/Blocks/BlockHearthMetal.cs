using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Products.Blocks;

/// <summary>
/// The metal standing in a furnace's hearth: the blast furnace's pig iron and the cupola's cast iron, as
/// one block with the metal in its code (<c>iiex:hearthmetal-{pigiron|castiron}</c>). Breaking it recovers
/// metal bits scaled to the stored count. It is both the residue a dead furnace leaves and the pool a
/// running one stands metal in; see <c>docs/design/layered-charge.md</c> § The crucible.
/// <para>
/// The released code <c>smex:solidifiediron</c> resolves here through <c>SmexToIiexMigration</c>, a row
/// asserted by <c>ReleasedCodeCoverageTests</c>. <see cref="BlockMigrations.HearthMetalMigration"/> covers
/// dev and playtest worlds only.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockHearthMetal : Block, IExBlockDefProvider {
  /// <summary>
  /// One blocktype, two metals. The textures differ so a dead blast furnace's bright sheet reads apart
  /// from a dead cupola's dull oxide at a glance, without a tooltip.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hearthmetal")
        .VariantGroup("metal", "pigiron", "castiron")
        .Class<BlockHearthMetal>()
        .EntityClass<BlockEntityHearthMetal>()
        // Two cells on one block: the crucible floor holds iron and the slag floating on it. Capacity is
        // set at runtime from IiexConfig rather than baked here, because the config is live-editable and
        // a number in JSON is not.
        .EntityBehavior<BEBehaviorMoltenCell>(
          new JObject
          {
            ["key"] = BlockEntityHearthMetal.IronCellKey,
            ["solidifies"] = true,
          }
        )
        .EntityBehavior<BEBehaviorMoltenCell>(
          new JObject
          {
            ["key"] = BlockEntityHearthMetal.SlagCellKey,
            ["solidifies"] = true,
          }
        )
        .Material(EnumBlockMaterial.Metal)
        .CreativeTab("general", "*")
        .CreativeTab("iiex", "*")
        .Shape("game:block/basic/cube")
        // Bright sheet for the blast furnace's pig, dull tarnish for the cupola's cast iron.
        .TextureByType("*-pigiron", "all", "game:block/metal/sheet-plain/iron5")
        .TextureByType("*-castiron", "all", "game:block/metal/tarnished/iron")
        .Resistance(45f)
        .MaxStackSize(8)
        .MiningTier(5)
        .MineTool(EnumTool.Pickaxe)
        .MetalSounds(),
    ];

  /// <summary>
  /// Drops the chipped-out solid for this block's metal, scaled by the stored count. The metal is read
  /// from the variant code and has no fallback, since a variant is always present. Both metals resolve to
  /// <c>game:metalbit-iron</c> today (mod alloys shatter to vanilla bits as scrap, their
  /// <c>solidDrop</c>), so there is no branch; giving either metal its own solid drop in the registry
  /// changes the result with no edit here.
  /// </summary>
  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    if (worldMap.BlockAccessor.GetBlockEntity(pos) is BlockEntityHearthMetal be) {
      Item? bit = worldMap.GetItem(
        MetalRegistry.SolidDropOf(MetalRegistry.MoltenItemOf(Variant["metal"]))
      );
      if (bit != null && be.MetalCount > 0) {
        return [new ItemStack(bit, be.MetalCount)];
      }
    }
    return base.GetDrops(worldMap, pos, byPlayer, dropQuantityMultiplier);
  }
}
