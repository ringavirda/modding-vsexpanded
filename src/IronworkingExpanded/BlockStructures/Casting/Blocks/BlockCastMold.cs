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
/// out and the mold stays (reusable). Cast in the sand cell from the matching pattern. Two tool types for
/// now: <c>plate</c> and <c>doubleingot</c> (no <c>quadrod</c> - rod is a rolled product).
/// </summary>
[BlockRegister]
public partial class BlockCastMold : Block, IExBlockDefProvider
{
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Mold(domain)];

  private static ExBlockDef Mold(string domain) =>
    ExBlockDef
      .Create(domain, "castmold")
      .Class<BlockCastMold>()
      .EntityClass<BlockEntityCastMold>()
      .Material(EnumBlockMaterial.Metal)
      .Resistance(3.5f)
      .MaxStackSize(16)
      .LightAbsorption(0)
      .VariantGroup("tooltype", "plate", "doubleingot")
      .ShapeByType("*-plate", "iwex:molten/molds/plate", rotateY: 90)
      .ShapeByType("*-doubleingot", "iwex:molten/molds/doubleingot", rotateY: 90)
      // The tray shapes bind their surface to "#other"; repaint it cast iron.
      .Texture("other", "iwex:block/metal/castiron")
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
      .RawByType("attributesByType", "castmold-plate", new { requiredUnits = 200, fillHeight = 1, drops = new[] { new { type = "item", code = "game:metalplate-{metal}" } } })
      .RawByType("attributesByType", "castmold-doubleingot", new { requiredUnits = 200, fillHeight = 1, drops = new[] { new { type = "item", code = "game:ingot-{metal}", quantity = 2 } } });

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
