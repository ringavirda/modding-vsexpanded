using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// An invisible, solid axle cell of the rolling mill: a <b>pass-through <c>mpenergy</c> node</b> that carries
/// the drive line across the mill's three-cell footprint, so the machine connects on both shaft ends (drivable
/// from either side, and stands chain on one line). It is not craftable - the mill places and clears it and
/// reroutes its break/drops/pick to the principal, exactly as <c>BlockStructureFiller</c> does for a plain
/// filler, but unlike a filler it IS a graph node (the network BFS only ever traverses
/// <see cref="BlockNetworkNode"/> cells, so a filler could never bridge the axle).
/// </summary>
[BlockRegister]
public partial class BlockRollingMillAxle : BlockNetworkNode, IExBlockDefProvider
{
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The axle-cell blocktype: invisible (empty shape), solid full-cube collision, no drops, hidden
  /// from the handbook. Two orientations (<c>ns</c>/<c>we</c>) so the mill can lay its axle along either
  /// horizontal axis; the connectors follow the orientation letters (<c>we</c> ⇒ east + west).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "rollingmillaxle")
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
        .VariantGroup("type", "shaft")
        .VariantGroup("orientation", "ns", "we"),
    ];

  #endregion

  private static BlockEntityRollingMillAxle? Axle(
    IWorldAccessor world,
    BlockPos pos
  ) => world.BlockAccessor.GetBlockEntity(pos) as BlockEntityRollingMillAxle;

  /// <summary>The mill owns this cell's orientation and lifecycle, so ignore neighbour changes: the base
  /// behaviour would recompute the orientation or self-break the cell (it has no resting surface of its own).</summary>
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
  )
  {
    // Breaking an axle cell breaks the whole mill: route to the principal, which clears every axle cell and
    // filler (mirrors BlockStructureFiller.OnBlockBroken). base.OnBlockBroken here would only RemoveNode this
    // one cell and leave the rest of the machine standing.
    BlockPos? principal = Axle(world, pos)?.Principal;
    if (
      principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
    )
    {
      mill.OnBlockBroken(world, principal, byPlayer, dropQuantityMultiplier);
      // Safety net: if the principal's break didn't clear us, don't linger as an orphaned graph node.
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

  /// <summary>Picking an axle cell yields the mill (via the principal), not this invisible internal block -
  /// and never the base's <c>GetDrops()[0]</c>, which is now empty.</summary>
  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
  {
    BlockPos? principal = Axle(world, pos)?.Principal;
    return principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
      ? mill.OnPickBlock(world, principal)
      : new ItemStack(this);
  }

  /// <summary>Shows the mill's info when the player looks at an axle cell.</summary>
  public override string GetPlacedBlockInfo(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer forPlayer
  )
  {
    BlockPos? principal = Axle(world, pos)?.Principal;
    return principal != null
      && world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill
      ? mill.GetPlacedBlockInfo(world, principal, forPlayer)
      : base.GetPlacedBlockInfo(world, pos, forPlayer);
  }
}
