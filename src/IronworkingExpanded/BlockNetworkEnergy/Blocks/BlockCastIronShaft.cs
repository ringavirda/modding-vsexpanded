using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// A straight cast-iron shaft: the mechanical-energy network's transmission run - the heavy-duty parallel to a
/// vanilla wooden axle. It carries the drive along a line and contributes a little rotational inertia
/// (<see cref="BlockEntityCastIronShaft"/>), so a shaft line itself buffers a touch. Bends and parallel splits
/// are the bevel/spur gears; this is the straight segment only, self-orienting on its axis (<c>ns</c>/<c>we</c>)
/// the way a straight pipe does - so an octagonal shaft laid in a line joins end to end.
/// </summary>
[BlockRegister]
public partial class BlockCastIronShaft : BlockNetworkNode, IExBlockDefProvider
{
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The cast-iron shaft blocktype: a thin octagonal bar on the run's axis, three orientations - two
  /// horizontal (<c>ns</c>/<c>we</c>) plus vertical (<c>ud</c>), so a run can climb. Authored along Z (<c>ns</c>),
  /// with <c>we</c> the 90° Y rotation and <c>ud</c> the 90° X rotation (matching the base box rotation).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "castironshaft", "mpenergy/castironshaft")
        .Class<BlockCastIronShaft>()
        .EntityClass<BlockEntityCastIronShaft>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(64)
        .Handbook("castironshaft-*")
        .VariantGroup("type", "shaft")
        .VariantGroup("orientation", "ns", "we", "ud")
        .ShapeByType("*-ns", "iwex:mpenergy/shaft", rotateY: 0)
        .ShapeByType("*-we", "iwex:mpenergy/shaft", rotateY: 90)
        .ShapeByType("*-ud", "iwex:mpenergy/shaft", rotateX: 90)
        .CreativeCommon("*-ns")
        // Thin central collision along the shaft axis (a shaft, not a wall); rotated per orientation by the base.
        .SingleCollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SingleSelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SideSolid(false)
        .SideOpaque(false),
    ];

  #endregion

  #region Turn a shaft into a bevel junction

  /// <summary>
  /// Using a bevel-gear item on a shaft turns it into a <see cref="BlockCastIronBevel"/> of the same axis - a
  /// branch-capable junction. No face or support to pick: the branches follow whatever perpendicular shafts the
  /// player then places against it. <c>SetBlock</c> swaps the BE class (shaft RemoveNode → bevel AddNode), and
  /// the bevel presents connectors on every face, so an already-adjacent perpendicular neighbour connects at once.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    ItemSlot? slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (!BlockCastIronBevel.IsBevelGearItem(slot?.Itemstack))
      return base.OnBlockInteractStart(world, byPlayer, blockSel);
    if (world.Side != EnumAppSide.Server)
      return true;

    if (
      world.GetBlock(CodeWithPath("castironbevel-bevel-" + (Orientation ?? "ns")))
      is not { } bevel
    )
      return true;
    world.BlockAccessor.SetBlock(bevel.BlockId, blockSel.Position);
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityCastIronBevel be
    )
      be.Orientation = Orientation;
    slot!.TakeOut(1);
    slot.MarkDirty();
    return true;
  }

  #endregion
}
