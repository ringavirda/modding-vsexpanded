using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// An invisible, solid axle cell of the rolling mill: a pass-through <c>mpenergy</c> node that carries the
/// drive line across the mill's three-cell footprint, so the machine connects on both shaft ends and stands
/// can be chained on one line. It is not craftable; the mill places and clears it, and its break, drops and
/// pick are rerouted to the principal as <c>BlockStructureFiller</c> does for a plain filler.
/// <para>
/// A footprint cell carrying a pass-through <c>BEBehaviorNetworkMember</c> now does the same job, so this
/// block is redundant. It stays because it is placed in existing worlds and retiring a placed block needs
/// a migration. See docs/design/mechanics/multiblock.md.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockRollingMillAxle
  : BlockNetworkNode,
    IExBlockDefProvider {
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The axle-cell blocktype: empty shape, solid full-cube collision, no drops, hidden from the
  /// handbook. Two orientations (<c>ns</c>/<c>we</c>) so the mill can lay its axle along either horizontal
  /// axis; the connectors follow the orientation letters (<c>we</c> gives east and west).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "forming", "forming/millaxle")
        .Class<BlockRollingMillAxle>()
        .EntityClass<BlockEntityRollingMillAxle>()
        .HandbookExclude()
        .Material(EnumBlockMaterial.Metal)
        .Shape("exlib:block/empty")
        .DrawType("json")
        .SideSolid(true)
        .SideOpaque(false)
        .LightAbsorption(0)
        .Resistance(45f)
        .NoDrops()
        .SingleCollisionBox(0f, 0f, 0f, 1f, 1f, 1f)
        .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
        .VariantGroup("type", "millaxle")
        .VariantGroup("orientation", "ns", "we")
        .NetworkOriented(),
    ];

  #endregion

  private static BlockEntityRollingMillAxle? Axle(
    IWorldAccessor world,
    BlockPos pos
  ) => world.BlockAccessor.GetBlockEntity(pos) as BlockEntityRollingMillAxle;

  /// <summary>Neighbour changes are ignored because the mill owns this cell's orientation and lifecycle:
  /// the base behaviour would recompute the orientation or self-break the cell, which has no resting
  /// surface of its own.</summary>
  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neighbour
  ) { }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    // Breaking an axle cell breaks the whole mill, so the break routes to the principal, which clears every
    // axle cell and filler (mirrors BlockStructureFiller.OnBlockBroken). The base implementation would only
    // RemoveNode this one cell and leave the rest of the machine standing.
    BlockPos? principal = Axle(world, pos)?.Principal;
    if (
      principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
    ) {
      mill.OnBlockBroken(world, principal, byPlayer, dropQuantityMultiplier);
      // If the principal's break left this cell standing, remove it so it is not an orphaned graph node.
      if (world.BlockAccessor.GetBlock(pos).Id == BlockId)
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
      return;
    }
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  /// <summary>The mill owns all drops; an axle cell drops nothing itself.</summary>
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  /// <summary>Picking an axle cell yields the mill via the principal, not this internal block. The base
  /// implementation would index into <c>GetDrops()</c>, which is empty here.</summary>
  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) {
    BlockPos? principal = Axle(world, pos)?.Principal;
    return
      principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
      ? mill.OnPickBlock(world, principal)
      : new ItemStack(this);
  }

  /// <summary>Shows the mill's info when the player looks at an axle cell.</summary>
  public override string GetPlacedBlockInfo(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer forPlayer
  ) {
    BlockPos? principal = Axle(world, pos)?.Principal;
    return
      principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
      ? mill.GetPlacedBlockInfo(world, principal, forPlayer)
      : base.GetPlacedBlockInfo(world, pos, forPlayer);
  }
}
