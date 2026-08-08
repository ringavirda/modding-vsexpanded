using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Casting.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The cast-iron casting mold block - the iron-tier replacement for the ceramic tool molds. A flat mold
/// placed on a surface and filled by a pour (crucible / ladle / mold pedestal, recognised through
/// <see cref="ILiquidMetalSink"/> on its entity); right-click with an empty hand takes the hardened cast
/// out and the mold stays (reusable). Cast in the sand cell from the matching pattern.
/// <para>
/// <b>One tool type: <c>ingot</c>, a single-ingot tray.</b> Anything the mill already makes has no mold
/// here: plates and rods are rolled products, so casting them in a tray would be a second route to the
/// mill's own output. The ingot mold survives because crucible steel has to be poured into something - a
/// tapped heat needs a vessel that is not a canal.
/// </para>
/// <para>
/// The drawn tray (<c>item-sandcast-ingotmold.json</c>) has one bay, so the block follows the art:
/// 100 units, one ingot.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockCastMold : Block, IExBlockDefProvider
{
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Mold(domain)];

  private static ExBlockDef Mold(string domain) =>
    ExBlockDef
      .Create(domain, "casting-mold", "casting/mold")
      .Class<BlockCastMold>()
      .EntityClass<BlockEntityCastMold>()
      .Material(EnumBlockMaterial.Metal)
      .Resistance(3.5f)
      .MaxStackSize(16)
      .LightAbsorption(0)
      // A one-state variant group on purpose: it keeps the `-ingot` suffix in the code, so adding a second
      // mold later is a new state rather than a code shape change every world has to migrate through.
      .VariantGroup("tooltype", "ingot")
      // The tray is the drawn `item-sandcast-ingotmold` art - one bay, and it carries its own cast-iron
      // texture, so no `#other` ceramic key needs binding.
      .ShapeByType("*-ingot", "iwex:molten/molds/ingot", rotateY: 90)
      .SingleCollisionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SingleSelectionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SideSolid(false)
      .SideOpaque(false)
      .CreativeTab("general", "*")
      .CreativeTab("iwex", "*")
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone")
      // The cast a full, hardened mold yields, resolved in the metal's cast domain by the entity.
      // 100 units, one ingot - vanilla's own ingot arithmetic, and what the single-bay tray draws.
      .RawByType("attributesByType", "casting-mold-ingot", new { requiredUnits = 100, fillHeight = 1, drops = new[] { new { type = "item", code = "game:ingot-{metal}", quantity = 1 } } });

  #endregion

  #region Interaction (take out the cast)

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCastMold mold
      && mold.TryTakeOut(byPlayer)
    )
      return true;
    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  #endregion

  #region Drops

  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    // The mold is reusable, so it drops itself - carrying its cast in the stack (so a filled mold can be
    // picked up and, if still liquid, spills when it leaves the hand). A hardened cast rides along.
    var moldStack = new ItemStack(this);
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCastMold mold
      && mold is { CurrentUnitAmount: > 0, MetalContent: { } metal }
    )
      ExpandedLib.Metals.MoltenContents.Write(
        moldStack,
        ExpandedLib.Metals.MoltenContents.MoldUnitsKey,
        metal,
        mold.CurrentUnitAmount
      );
    return [moldStack];
  }

  #endregion

  #region Heat-sink glow

  /// <summary>
  /// Emits incandescent block light off the hotter of the cast and the mold body, so a freshly-poured iron
  /// mold glows and lights its surroundings even as the casting inside sets - the same
  /// <c>GetLightHsv</c> + <c>MarkBlockDirty</c>-on-change idiom as the molten barrel and the canals. The
  /// entity owns the level (<see cref="BlockEntityCastMold.GlowLightLevel"/>) and re-lights on change.
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  )
  {
    if (pos != null && blockAccessor.GetBlockEntity(pos) is BlockEntityCastMold mold)
    {
      byte val = mold.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  #endregion
}
