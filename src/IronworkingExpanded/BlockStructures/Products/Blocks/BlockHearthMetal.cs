using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Products.Blocks;

/// <summary>
/// The metal standing in a furnace's hearth: the blast furnace's pig iron and the cupola's cast iron, as
/// one block with the metal in its code (<c>iwex:hearthmetal-{pigiron|castiron}</c>). Breaking it recovers
/// metal bits scaled to the stored count.
/// <para>
/// <b>Replaces the separate solidified-iron and solidified-cast-iron blocks with one variant group.</b>
/// The group renames no released code:
/// <c>ExpandedLib.Testing.ReleasedCodes</c> has <b>no iwex rows at all</b>, so neither predecessor ever
/// shipped and neither carries released debt.
/// <see cref="BlockMigrations.HearthMetalMigration"/> exists as a courtesy to dev and playtest worlds, not
/// as a contract. The one genuinely released code in this family is <c>smex:solidifiediron</c>, and it
/// reaches this block through <c>SmexToIwexMigration</c> - that row is load-bearing and is asserted by
/// <c>ReleasedCodeCoverageTests</c>.
/// </para>
/// <para>
/// The name follows the model, not only the code. This block is not only the residue a
/// dead furnace leaves: <c>docs/design/layered-charge.md</c> § <i>The crucible</i> makes it the live pool a
/// running furnace stands metal in, placed the moment melting starts rather than at extinguish. That
/// work is still to come; merging the two identities gives it one block to make live.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockHearthMetal : Block, IExBlockDefProvider
{
  /// <summary>
  /// One blocktype, two metals. The textures are carried verbatim from the two blocks this replaces and
  /// the distinction is deliberate: a dead blast furnace's bright sheet must read apart from a dead
  /// cupola's dull oxide at a glance, without a tooltip.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hearthmetal")
        .VariantGroup("metal", "pigiron", "castiron")
        .Class<BlockHearthMetal>()
        .EntityClass<BlockEntityHearthMetal>()
        .Material(EnumBlockMaterial.Metal)
        .CreativeTab("general", "*")
        .CreativeTab("iwex", "*")
        .Shape("game:block/basic/cube")
        // Bright sheet for the blast furnace's pig, dull tarnish for the cupola's cast iron - the same
        // two textures the merged blocks used, and the same reason.
        .TextureByType("*-pigiron", "all", "game:block/metal/sheet-plain/iron5")
        .TextureByType("*-castiron", "all", "game:block/metal/tarnished/iron")
        .Resistance(45f)
        .MaxStackSize(8)
        .MiningTier(5)
        .MineTool(EnumTool.Pickaxe)
        .MetalSounds(),
    ];

  /// <summary>
  /// Drops the chipped-out solid for this block's metal, scaled by the stored count.
  /// <para>
  /// <b>The metal comes from the code, and there is deliberately no fallback.</b> An attribute-based
  /// metal with a default is how an
  /// attribute-less cupola block could quietly drop the blast furnace's metal. With the metal in the
  /// variant there is no attribute-less case to default for, so no default constant is carried.
  /// </para>
  /// <para>
  /// Both metals resolve to <c>game:metalbit-iron</c> today - mod alloys shatter to vanilla bits as
  /// scrap (their <c>solidDrop</c>) - so there is no branch here. That is a fact about the registry, not
  /// about this block: give either metal its own solid drop and this starts differing with no edit.
  /// </para>
  /// </summary>
  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    if (worldMap.BlockAccessor.GetBlockEntity(pos) is BlockEntityHearthMetal be)
    {
      Item? bit = worldMap.GetItem(
        MetalRegistry.SolidDropOf(MetalRegistry.MoltenItemOf(Variant["metal"]))
      );
      if (bit != null && be.MetalCount > 0)
      {
        return [new ItemStack(bit, be.MetalCount)];
      }
    }
    return base.GetDrops(worldMap, pos, byPlayer, dropQuantityMultiplier);
  }
}
