using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The cast-iron casting mold block, the iron-tier counterpart to the ceramic tool molds. A flat mold
/// placed on a surface and filled by a pour (crucible, ladle or mold pedestal, which reach it through
/// <see cref="Vintagestory.GameContent.ILiquidMetalSink"/> on its entity); right-click with an empty hand takes the hardened cast
/// out and leaves the mold, which is reusable. The mold itself is cast in the sand cell from the matching
/// pattern. One tool type, <c>ingot</c>: a single-bay tray of 100 units yielding one ingot.
/// </summary>
[BlockRegister]
public partial class BlockCastMold : Block, IExBlockDefProvider {
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
      // A one-state variant group keeps the `-ingot` suffix in the block code, so a second mold type is a
      // new state rather than a code shape change worlds have to migrate through.
      .VariantGroup("tooltype", "ingot")
      // The tray uses the `item-sandcast-ingotmold` art, which carries its own cast-iron texture, so no
      // `#other` ceramic key is bound.
      .ShapeByType("*-ingot", "iiex:molten/molds/ingot", rotateY: 90)
      .SingleCollisionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SingleSelectionBox(0.0625f, 0f, 0.0625f, 0.9375f, 0.125f, 0.9375f)
      .SideSolid(false)
      .SideOpaque(false)
      .CreativeTab("general", "*")
      .CreativeTab("iiex", "*")
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone")
      // The cast a full, hardened mold yields; the entity resolves it in the metal's cast domain.
      // 100 units per ingot, matching vanilla's ingot arithmetic.
      .RawByType(
        "attributesByType",
        "casting-mold-ingot",
        new {
          requiredUnits = 100,
          fillHeight = 1,
          drops = new[]
          {
            new
            {
              type = "item",
              code = "game:ingot-{metal}",
              quantity = 1,
            },
          },
        }
      );

  #endregion

  #region Interaction (take out the cast)

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
        is BlockEntityCastMold mold
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
  ) {
    // The mold is reusable, so it drops itself and carries its cast in the stack: a filled mold can be
    // picked up, and while the metal is still liquid it spills once it leaves the hand.
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
  /// Emits incandescent block light off the hotter of the cast and the mold body, so a freshly poured mold
  /// keeps lighting its surroundings while the casting sets. The entity owns the level
  /// (<see cref="BlockEntityCastMold.GlowLightLevel"/>) and re-lights the block when it changes.
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  ) {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityCastMold mold
    ) {
      byte val = mold.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  #endregion
}
