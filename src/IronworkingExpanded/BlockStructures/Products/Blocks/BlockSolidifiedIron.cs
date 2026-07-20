using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Products.Blocks;

/// <summary>
/// The frozen melt a furnace leaves behind when it is extinguished mid-heat; breaking it recovers metal
/// bits scaled to the stored count. Backs BOTH metals - the blast furnaces' iron and the cupola's cast
/// iron - as two definitions over one class, with the metal supplied as a block attribute.
/// <para>
/// Deliberately NOT a variant group: <c>VariantGroup("metal", ...)</c> would rename the shipped
/// <c>iwex:solidifiediron</c> to <c>iwex:solidifiediron-iron</c> and orphan every one already placed in a
/// world. Deliberately not a second class either - that is the cold/hot furnace duplication this branch
/// already paid for. Data, not code: the shared furnace core reads the product off the block, so a
/// subclass supplies only which block to place.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockSolidifiedIron : Block, IExBlockDefProvider
{
  // Metal token for a block saved before the attribute existed - those are all blast-furnace iron.
  private const string DefaultMetal = "iron";

  /// <summary>
  /// Code-first blocktype definitions (the iron one migrated verbatim from the former
  /// <c>assets/iwex/blocktypes/blastfurnace/solidifiediron.json</c>, 2026-07-14). Discovered and
  /// injected by the shared definition system; kept next to the class it configures.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      Solid(
        domain,
        "solidifiediron",
        "game:block/metal/sheet-plain/iron5",
        "iron"
      ),
      // Dull oxide grey against the iron block's bright sheet, so a dead cupola's residue reads as a
      // different material from a dead blast furnace's at a glance (same texture cast iron uses as an
      // ingot/plate).
      Solid(
        domain,
        "solidifiedcastiron",
        "game:block/metal/tarnished/iron",
        "castiron"
      ),
    ];

  // The surface both share; only the texture and the metal token differ.
  private static ExBlockDef Solid(
    string domain,
    string code,
    string texture,
    string metal
  ) =>
    ExBlockDef
      .Create(domain, code)
      .Class<BlockSolidifiedIron>()
      .EntityClass<BlockEntitySolidifiedIron>()
      .Material(EnumBlockMaterial.Metal)
      .CreativeTab("general", "*")
      .CreativeTab("iwex", "*")
      .Shape("game:block/basic/cube")
      .TextureAll(texture)
      .Resistance(45f)
      .MaxStackSize(8)
      .MiningTier(5)
      .MineTool(EnumTool.Pickaxe)
      .MetalSounds()
      .Attribute("metal", metal);

  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    if (
      worldMap.BlockAccessor.GetBlockEntity(pos) is BlockEntitySolidifiedIron be
    )
    {
      // The chipped-out solid for this block's metal via the registry (registered SolidDrop else the
      // ingot-X -> metalbit-X convention), so cast iron yields iwex:metalbit-castiron and iron yields
      // game:metalbit-iron with no branch here.
      string metal =
        Attributes?["metal"].AsString(DefaultMetal) ?? DefaultMetal;
      Item? bit = worldMap.GetItem(
        MetalRegistry.SolidDropOf(MetalRegistry.MoltenItemOf(metal))
      );
      if (bit != null && be.MetalCount > 0)
      {
        return [new ItemStack(bit, be.MetalCount)];
      }
    }
    return base.GetDrops(worldMap, pos, byPlayer, dropQuantityMultiplier);
  }
}
