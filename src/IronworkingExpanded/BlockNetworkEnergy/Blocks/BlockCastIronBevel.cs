using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// A cast-iron shaft that has been turned into a <b>bevel junction</b>: it branches the mpenergy run onto any
/// perpendicular face. Unlike a plain shaft (which joins only along its axis), a bevel presents a connector on
/// <b>every</b> face, so placing a shaft against a perpendicular side auto-connects it and grows a bevel gear
/// there - no per-face gear items, exactly like a vanilla angled gear except it allows several angled outputs
/// (west/east/up/down) plus the axis continuations at once. A bevel is reached by using one
/// <see cref="GearItemCode"/> on a shaft; the geared faces after that are DERIVED from the connected neighbours
/// (<see cref="BlockEntityCastIronBevel"/>), not stored.
/// </summary>
[BlockRegister]
public partial class BlockCastIronBevel : BlockNetworkNode, IExBlockDefProvider
{
  public override string NetworkType => "mpenergy";

  /// <summary>The item consumed once to turn a shaft into a bevel.</summary>
  public const string GearItemCode = "iwex:bevelgear";

  #region Code-first definition

  /// <summary>The bevel blocktype: same axis model as the shaft (type <c>bevel</c> × orientation
  /// <c>ns</c>/<c>we</c>/<c>ud</c>). Its world mesh is composed at runtime (shaft body + one gear per connected
  /// perpendicular face) by <see cref="BlockEntityCastIronBevel.OnTesselation"/>, so the declared shape here is
  /// only the fallback - the bare shaft. A bevel is reached by adding a gear to a shaft; it stays a bevel (a
  /// branch-capable junction) until broken.</summary>
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
        // Must mirror the shaft's three orientations: a bevel is grown FROM a shaft of the same axis, so a
        // missing variant means that axis can never branch (a `ud` run could climb but never turn off).
        .VariantGroup("orientation", "ns", "we", "ud")
        .ShapeByType("*-ns", "iwex:mpenergy/shaft", rotateY: 0)
        .ShapeByType("*-we", "iwex:mpenergy/shaft", rotateY: 90)
        .ShapeByType("*-ud", "iwex:mpenergy/shaft", rotateX: 90)
        .SingleCollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SingleSelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
        .SideSolid(false)
        .SideOpaque(false),
    ];

  #endregion

  #region Connectors (a full junction)

  /// <summary>A bevel is a junction: it connects on every face - both axis continuations and the four
  /// perpendicular branches. The reciprocal check on the neighbour side decides where an actual run forms.</summary>
  public override bool HasConnectorAt(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) => true;

  #endregion

  #region Geared-face helpers (shared with the BE's rendering derivation)

  /// <summary>A face is a perpendicular branch (a gear can grow there) when it is NOT one of the shaft axis
  /// ends - the ends carry the through-run. <paramref name="orientation"/> is the <c>ns</c>/<c>we</c> axis.</summary>
  public static bool IsPerpendicular(string? orientation, BlockFacing face) =>
    orientation != null && !orientation.Contains(face.Code[0]);

  /// <summary>Whether a connected mpenergy neighbour faces this bevel across <paramref name="face"/> - an
  /// axle/bevel on the other side reciprocating the connector. This is what makes a gear appear on that face.</summary>
  public static bool HasConnectedNeighbor(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  )
  {
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
  /// base support/orientation handling. The geared faces are derived in <see cref="BlockEntityCastIronBevel.OnTesselation"/>.</summary>
  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neighbour
  )
  {
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
  )
  {
    var drops = new List<ItemStack>();
    if (
      world.GetBlock(CodeWithPath("mpenergy-shaft-" + (Orientation ?? "ns")))
      is { } shaft
    )
      drops.Add(new ItemStack(shaft));
    if (world.GetItem(new AssetLocation(GearItemCode)) is { } gearItem)
      drops.Add(new ItemStack(gearItem));

    return [.. drops];
  }

  #endregion
}
