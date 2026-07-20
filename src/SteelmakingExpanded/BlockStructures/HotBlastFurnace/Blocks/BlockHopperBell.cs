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
public partial class BlockHopperBell : Block, IExBlockDefProvider
{
  /// <summary>The bell hopper blocktype, authored in C# (migrated from blastfurnace/hopperbell.json).</summary>
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
  )
  {
    var drops = new List<ItemStack>(
      base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)
    );

    // Return the blast mix buffered in the internal magazine so it isn't lost.
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperBell be
      && be.BlastMixMagazine > 0
    )
    {
      Item? blastmix = world.GetItem(new AssetLocation("iwex", "blastmix"));
      if (blastmix != null)
        drops.Add(new ItemStack(blastmix, be.BlastMixMagazine));
    }

    return drops.ToArray();
  }
}
