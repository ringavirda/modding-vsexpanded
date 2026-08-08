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
/// The firebox: a refractory rim with cast-iron firebars across it, holding the bed of fuel a
/// reverberatory furnace draws its flame off. Replaces free-placed <c>game:coalpile</c> in the suite's
/// fuel-bed machines. The firebars are part of the block, so no separate grating cell sits under a
/// firebox. See <c>docs/design/machines/firebox.md</c>.
/// <para>
/// Its orientation is visual only: the facing turns which way the bars run and nothing else. No layout
/// may orientation-check a firebox cell, and no code may read a direction off it.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFirebox : Block, IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The firebox blocktype. Available in every refractory tier, since a fuel bed carries no heat
  /// requirement of its own, and it wears whichever tier it was built from, the same <c>{tier}</c> the
  /// cores use.
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
        // Build outline: a firebox is a functional cell of a furnace layout, so a player standing at it
        // can preview and complete an unfinished furnace, as at a door, a tap or a charge pile.
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
        // All four fuels are declared; the block entity repoints the bed's faces at whichever one was
        // charged (ExShapeElements.Retextured), so a bed of charcoal does not look like a bed of coke.
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
  /// Which declared texture the bed wears for <paramref name="fuelCode"/>. Matched on the code rather
  /// than a resolved collectible, as <c>BlockChargePile.ElementOf</c> is: a bed stores a code and must
  /// keep drawing after the mod that owned that fuel is removed. The function is total, so no fuel can
  /// fail to draw.
  /// </summary>
  public static string TextureKeyOf(string? fuelCode) {
    if (string.IsNullOrEmpty(fuelCode))
      return FuelTexture;
    if (fuelCode.Contains("bituminous", StringComparison.Ordinal))
      return "bituminous";
    if (fuelCode.Contains("anthracite", StringComparison.Ordinal))
      return "anthracite";
    if (fuelCode.Contains("charcoal", StringComparison.Ordinal))
      return "charcoal";
    // Coke, and the fallback for any fuel a later mod adds: an unknown fuel draws as coke rather than
    // as the engine's missing-texture placeholder.
    return FuelTexture;
  }

  /// <summary>The element paths a bed of <paramref name="layers"/> courses draws: the base always, then
  /// one <c>CokeLn</c> per standing course.</summary>
  public static List<string> ElementsFor(int layers) {
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
  /// Drops the block plus whatever fuel was still in the bed. A bed holds one fuel, so that is a single
  /// extra stack rather than one per material.
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) {
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFirebox be
      && be.Bed?.Contents() is { } fuel
    ) {
      be.Bed.Clear();
      world.SpawnItemEntity(fuel, pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion

  #region Interaction

  /// <summary>
  /// Charges with fuel in hand, takes a course back out with an empty one. A deposit is spread across
  /// every firebox cell of the owning furnace: one interaction fills the whole bed but costs per cell, so
  /// a two-cell reheat firebox costs twice a one-cell puddling firebox for the same visible fill. A
  /// firebox with no owning furnace fills only itself. See <c>docs/design/machines/firebox.md</c>.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityFirebox be
    )
      return false;

    // The build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace instead
    // of charging it.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        blockSel.Position
      )
    )
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (active is { Empty: false }) {
      if (!BEBehaviorFirebox.IsFuel(active.Itemstack)) {
        (byPlayer as IServerPlayer)?.SendIngameError("iwex-firebox-notfuel");
        return true;
      }
      int taken = be.Charge(active.Itemstack);
      if (taken == 0) {
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
      world.SpawnItemEntity(
        course,
        blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
      );
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
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
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is BlockEntityFirebox be
      && be.ResolveOwningAnchor() is { StructureComplete: false }
    )
      help.AddRange(BlockBehaviorMultiblockStructure.ProjectionHelp(this));
    return [.. help];
  }

  #endregion
}
