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
    // Null-guarded: vanilla's Block.GetDrops returns null when the block declares no drops, and wrapping
    // that in a List<> ctor throws ArgumentNullException on null. A shipped block always has its
    // self-drop, so the case never fires in play - but a definition change that dropped the entry would
    // turn "break this block" into a crash rather than into a block that drops nothing.
    ItemStack[]? inherited = base.GetDrops(
      world,
      pos,
      byPlayer,
      dropQuantityMultiplier
    );
    var drops = new List<ItemStack>(inherited ?? []);

    // Return whatever the magazine is actually holding, so a broken bell does not eat its own load.
    //
    // The magazine's own stack is the honest answer and needs no lookup at all: it is already a
    // resolved stack of exactly what is in there, grade and all. Do not swap this for an item minted
    // through a null-guarded `world.GetItem(...)`: that hands back a different item from the burden the
    // bell buffers (losing the grade the ore mixer stamped), and the null guard fails soft - deleting
    // the looked-up item would make a break silently return nothing, with the build still succeeding
    // and no test going red.
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperBell be
      && be.MagazineContents is { StackSize: > 0 } magazine
    )
      drops.Add(magazine.Clone());

    return drops.ToArray();
  }
}
