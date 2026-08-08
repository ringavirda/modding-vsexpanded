using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The <b>firebox</b>: a refractory rim with cast-iron firebars across it, holding a bed of fuel that a
/// reverberatory furnace draws its flame off. The block that replaces free-placed <c>game:coalpile</c> in
/// every fuel-bed machine in the suite - see <c>docs/design/machines/firebox.md</c>.
/// <para>
/// <b>The firebars are part of the block</b>, which is why <c>game:refractorybrickgrating</c> left the
/// puddling and reheat layouts: the grate stopped being a separate cell. It also makes the firebox a
/// <b>tech-tree edge rather than a loop</b> - the bars are a sand-cast part, and the machines that have a
/// firebox are not the machines that make cast iron, so the dependency runs one way (blast furnace or
/// cupola → iron → rods → firebox → reverberatory furnaces). That is the historical order.
/// </para>
/// <para>
/// <b>Its orientation is visual only.</b> The facing turns which way the bars run and nothing else - no
/// layout may orientation-check a firebox cell, and no code may read a direction off it. Stated here
/// because a block carrying a <c>side</c> group looks exactly like one whose facing is load-bearing, and
/// the charge lid two files over <em>is</em> that.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFirebox : Block, IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>
  /// The firebox blocktype.
  /// <para>
  /// <b>Any refractory tier.</b> A firebox is a fuel bed, not a metallurgical shell, so there is no heat
  /// argument for pinning tier 3 - and pinning it would put the block out of reach of exactly the
  /// early-tier player who needs a puddling hearth first. It wears whichever tier it was built from, the
  /// same <c>{tier}</c> the cores use.
  /// </para>
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/firebox")
        .Class<BlockFirebox>()
        .EntityClass<BlockEntityFirebox>()
        .EntityBehavior<BEBehaviorFirebox>()
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(4)
        // The build outline: a firebox is a functional cell of a furnace layout, so a player standing at
        // it can preview and complete an unfinished furnace - as at a door, a tap or a charge pile.
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iwex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode and N7).
        .VariantGroup("type", "firebox")
        .VariantGroup("tier", "tier1", "tier2", "tier3")
        .SideVariant()
        .ShapeByTypePerOrientation("iwex:furnace/firebox", 0)
        // The rim wears what it was built from; the bars are cast iron whatever the brick is.
        .Texture("front1", "game:block/clay/refractory/{tier}/front1")
        .Texture("burned", "game:block/clay/vessel/sides/burned")
        .Texture("cast-iron1", "iwex:block/metal/castiron")
        // All four fuels are declared, and the block entity repoints the bed's faces at whichever one
        // was charged (ExShapeElements.Retextured). Vanilla already names every one of them, so a firebox
        // of charcoal and a firebox of coke stop looking alike for the cost of three extra keys.
        .Texture(FuelTexture, "game:block/coal/coke")
        .Texture("bituminous", "game:block/coal/bituminous")
        .Texture("anthracite", "game:block/coal/anthracite")
        .Texture("charcoal", "game:block/coal/charcoal")
        .CreativeCommon("*-tier1-n")
        .Replaceable(400)
        .Resistance(3.5f)
        // Loose fuel over an open grate: the furnace's own light has to reach through the bed rather than
        // being absorbed by it, and the bars must not cull the brickwork behind them.
        .LightAbsorption(0)
        .SolidNonOpaque()
        .Sound("walk", "walk/stone")
        .Sound("place", "block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        ),
    ];

  #endregion

  #region Shape

  /// <summary>The always-drawn part: the refractory rim and the cast-iron bars.</summary>
  public const string BaseElement = "Base";

  /// <summary>The fuel bed's group. Its children are <c>CokeL1</c>..<c>CokeL6</c>, bottom-first.</summary>
  public const string BedElement = "Coke";

  /// <summary>The texture key every bed face is authored against, and therefore the one
  /// <see cref="TextureKeyOf"/> repoints away from.</summary>
  public const string FuelTexture = "coke";

  /// <summary>
  /// Which declared texture the bed wears for <paramref name="fuelCode"/>. Decided on the <b>code</b>
  /// rather than a resolved collectible, for the reason <c>BlockChargePile.ElementOf</c> is: a bed stores
  /// a code and has to keep drawing after the mod that owned one is removed, and it makes the function
  /// total - no fuel can fail to draw.
  /// </summary>
  public static string TextureKeyOf(string? fuelCode)
  {
    if (string.IsNullOrEmpty(fuelCode))
      return FuelTexture;
    if (fuelCode.Contains("bituminous", StringComparison.Ordinal))
      return "bituminous";
    if (fuelCode.Contains("anthracite", StringComparison.Ordinal))
      return "anthracite";
    if (fuelCode.Contains("charcoal", StringComparison.Ordinal))
      return "charcoal";
    // Coke, and the honest fallback for anything a later mod adds: an unknown fuel draws as the
    // metallurgical default rather than as the engine's missing-texture pink.
    return FuelTexture;
  }

  /// <summary>The element paths a bed of <paramref name="layers"/> courses draws - the base always, then
  /// one <c>CokeL</c><i>n</i> per standing course.</summary>
  public static List<string> ElementsFor(int layers)
  {
    var keep = new List<string> { BaseElement };
    for (int i = 1; i <= layers; i++)
      keep.Add(BedElement + "/CokeL" + i);
    return keep;
  }

  #endregion

  /// <summary>The bars turn with the block; nothing else does. See the class remark.</summary>
  public int StructureAngle =>
    ExpandedLib.Helpers.ExOrientation.AngleFromSide(Variant["side"]);

  #region Break safety

  /// <summary>
  /// Drops the block itself plus whatever fuel was still in the bed - a firebox is not a one-way sink, and
  /// a bed holds one fuel, so this is a single extra stack rather than one per material.
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  )
  {
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFirebox be
      && be.Bed?.Contents() is { } fuel
    )
    {
      be.Bed.Clear();
      world.SpawnItemEntity(fuel, pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion

  #region Interaction

  /// <summary>
  /// Charge with fuel in hand, take a course back out with an empty one.
  /// <para>
  /// <b>A deposit is spread across every firebox cell of the owning furnace</b>, which is what
  /// <c>firebox.md</c> means by one pool: the player interacts once and fills the whole bed, but pays per
  /// cell - so a two-cell reheat firebox costs twice a one-cell puddling firebox for the same visible
  /// fill. A firebox with no furnace fills only itself, which is the honest answer for a bed standing in
  /// a field.
  /// </para>
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityFirebox be)
      return false;

    // The build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace instead
    // of shovelling coke into it.
    if (BlockBehaviorMultiblockStructure.TryToggleProjection(world, byPlayer, blockSel.Position))
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (active is { Empty: false })
    {
      if (!BEBehaviorFirebox.IsFuel(active.Itemstack))
      {
        (byPlayer as IServerPlayer)?.SendIngameError("iwex-firebox-notfuel");
        return true;
      }
      int taken = be.Charge(active.Itemstack);
      if (taken == 0)
      {
        (byPlayer as IServerPlayer)?.SendIngameError(
          be.Bed?.Accepts(active.Itemstack) == true
            ? "iwex-firebox-full"
            : "iwex-firebox-wrongfuel"
        );
        return true;
      }
      active.TakeOut(taken);
      active.MarkDirty();
      return true;
    }

    if (be.Bed?.TryTakeLayer() is not { } course)
      return true;
    if (byPlayer.InventoryManager?.TryGiveItemstack(course) != true)
      world.SpawnItemEntity(course, blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:firebox-help-charge",
        MouseButton = EnumMouseButton.Right,
      },
      new()
      {
        ActionLangCode = "iwex:firebox-help-take",
        MouseButton = EnumMouseButton.Right,
      },
    };
    if (
      world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityFirebox be
      && be.ResolveOwningAnchor() is { StructureComplete: false }
    )
      help.AddRange(BlockBehaviorMultiblockStructure.ProjectionHelp(this));
    return [.. help];
  }

  #endregion
}
