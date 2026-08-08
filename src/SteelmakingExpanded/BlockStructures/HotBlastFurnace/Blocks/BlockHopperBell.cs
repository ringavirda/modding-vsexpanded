using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;

/// <summary>
/// The bell hopper block; its <see cref="BlockEntityHopperBell"/> crafts blast
/// mix and drops it into the furnace.
/// </summary>
[BlockRegister]
public partial class BlockHopperBell : Block, IExBlockDefProvider {
  /// <summary>The bell hopper blocktype.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopperbell", "blastfurnace/hopperbell")
        .Class<BlockHopperBell>()
        .EntityClass("smex.BlockEntityHopperBell")
        .Behavior("Lockable")
        .CreativeCommon("*")
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .LightAbsorption(0)
        .Shape("smex:blastfurnace/hopper-bell")
        .NonSolid()
        .Resistance(1.75f)
        .MetalSounds(),
    ];

  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    // Null-guarded: Block.GetDrops returns null when the block declares no drops, and the List<>
    // constructor throws ArgumentNullException on null.
    ItemStack[]? inherited = base.GetDrops(
      world,
      pos,
      byPlayer,
      dropQuantityMultiplier
    );
    var drops = new List<ItemStack>(inherited ?? []);

    // Drop the magazine's own stack so a broken bell does not eat its load. Clone it rather than
    // minting an item through world.GetItem: the stored stack already carries the grade the ore
    // mixer stamped, and a lookup that resolves to null would silently drop the load instead.
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperBell be
      && be.MagazineContents is { StackSize: > 0 } magazine
    )
      drops.Add(magazine.Clone());

    return drops.ToArray();
  }
}
