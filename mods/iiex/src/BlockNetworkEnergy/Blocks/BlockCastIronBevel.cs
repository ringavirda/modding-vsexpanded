using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// A cast-iron shaft turned into a bevel junction: it branches the mpenergy run onto any perpendicular face.
/// Where a plain shaft joins only along its axis, a bevel presents a connector on every face, so a shaft placed
/// against a perpendicular side connects and grows a gear there. Several angled branches
/// (west/east/up/down) and the two axis continuations can run at once. A shaft becomes a bevel by using one
/// <see cref="GearItemCode"/> on it; the geared faces are then derived from the connected neighbours
/// (<see cref="BlockEntityCastIronBevel"/>), not stored.
/// </summary>
[BlockRegister]
public partial class BlockCastIronBevel : BlockNetworkNode, IExBlockDefProvider {
  public override string NetworkType => "mpenergy";

  /// <summary>The item consumed once to turn a shaft into a bevel.</summary>
  public const string GearItemCode = "iiex:bevelgear";

  #region Code-first definition

  /// <summary>The bevel blocktype: same axis model as the shaft (type <c>bevel</c> × orientation
  /// <c>ns</c>/<c>we</c>/<c>ud</c>). The world mesh is composed at runtime by
  /// <see cref="BlockEntityCastIronBevel.OnTesselation"/>, so the shape declared here is only the fallback bare
  /// shaft. The block stays a bevel until broken.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "mpenergy", "mpenergy/bevel")
        .Class<BlockCastIronBevel>()
        .EntityClass<BlockEntityCastIronBevel>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(64)
        .HandbookExclude() // not craftable/placed directly - grown from a shaft + gear
        .VariantGroup("type", "bevel")
        // Must mirror the shaft's three orientations: a bevel is grown from a shaft of the same axis, so a
        // missing variant means that axis can never branch (a `ud` run could climb but never turn off).
        .VariantGroup("orientation", "ns", "we", "ud")
        .NetworkOriented()
        .ShapeByType("*-ns", "iiex:mpenergy/shaft", rotateY: 0)
        .ShapeByType("*-we", "iiex:mpenergy/shaft", rotateY: 90)
        .ShapeByType("*-ud", "iiex:mpenergy/shaft", rotateX: 90)
        .SingleCollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SingleSelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SideSolid(false)
        .SideOpaque(false),
    ];

  #endregion

  #region Connectors (a full junction)

  /// <summary>A bevel connects on every face: both axis continuations and the four perpendicular branches. The
  /// reciprocal check on the neighbour side decides where a run actually forms.</summary>
  public override bool HasConnectorAt(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) => true;

  #endregion

  #region Geared-face helpers (shared with the BE's rendering derivation)

  /// <summary>A face is a perpendicular branch, so a gear can grow there, when it is not one of the shaft axis
  /// ends, which carry the through-run. <paramref name="orientation"/> is the <c>ns</c>/<c>we</c> axis.</summary>
  public static bool IsPerpendicular(string? orientation, BlockFacing face) =>
    orientation != null && !orientation.Contains(face.Code[0]);

  /// <summary>Whether an mpenergy neighbour across <paramref name="face"/> reciprocates the connector. A gear
  /// is drawn on the face only when it does.</summary>
  public static bool HasConnectedNeighbor(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) {
    BlockPos nPos = pos.AddCopy(face);
    return world.GetBlock(nPos) is BlockNetworkNode neighbor
      && neighbor.NetworkType == "mpenergy"
      && neighbor.HasConnectorAt(world, nPos, face.Opposite);
  }

  /// <summary>True when <paramref name="stack"/> is the bevel-gear item used to turn a shaft into a bevel.</summary>
  public static bool IsBevelGearItem(ItemStack? stack) =>
    stack?.Collectible?.Code?.ToString() == GearItemCode;

  #endregion

  #region Re-render on neighbour change

  /// <summary>A branch appears or disappears when a neighbour is placed or broken, so re-tesselate on top of the
  /// base support and orientation handling. The geared faces are derived in
  /// <see cref="BlockEntityCastIronBevel.OnTesselation"/>.</summary>
  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neighbour
  ) {
    base.OnNeighbourBlockChange(world, pos, neighbour);
    world.BlockAccessor.GetBlockEntity(pos)?.MarkDirty(true);
  }

  #endregion

  #region Drops (the shaft + the one gear it was made from)

  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    var drops = new List<ItemStack>();
    if (
      world.GetBlock(CodeWithPath("mpenergy-shaft-" + (Orientation ?? "ns"))) is { } shaft
    )
      drops.Add(new ItemStack(shaft));
    if (world.GetItem(new AssetLocation(GearItemCode)) is { } gearItem)
      drops.Add(new ItemStack(gearItem));

    return [.. drops];
  }

  #endregion
}
