using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// Network-port access shared by every fixed machine that reads or feeds a block network through a
/// connector face (boiler, engine, sub-machines, pumps, intake, converter, cowper). A machine port is
/// the network in the cell across the connector face; these extensions are the single place that rule
/// is implemented.
/// </summary>
public static class MachinePorts {
  /// <summary>The block-network manager, resolved from the entity's API (cached by the mod loader).</summary>
  public static BlockNetworkModSystem? NetworkSystem(this BlockEntity be) =>
    be.Api?.ModLoader.GetModSystem<BlockNetworkModSystem>();

  /// <summary>
  /// The network of type <typeparamref name="TNet"/> across <paramref name="connectorFace"/> from this
  /// machine, or <c>null</c> when the adjacent block exposes no connector facing back, so a machine
  /// never draws from an unplumbed line. Shorthand for <see cref="ConnectedNetworkAt{TNet}"/> read from
  /// the machine's own cell.
  /// </summary>
  public static TNet? ConnectedNetwork<TNet>(
    this BlockEntity be,
    BlockFacing connectorFace
  )
    where TNet : BlockNetwork =>
    be.ConnectedNetworkAt<TNet>(be.Pos, connectorFace);

  /// <summary>
  /// The network of type <typeparamref name="TNet"/> across <paramref name="connectorFace"/> from
  /// <paramref name="at"/>, which need not be this machine's own cell: a mega-block couples on a
  /// footprint port two cells from the block that owns it, and the reciprocal-connector test has to run
  /// from the port rather than from the principal. Returns <c>null</c> when the adjacent block exposes
  /// no connector facing back, so a machine never draws from an unplumbed line. The reciprocal-connector
  /// test lives in <see cref="BlockNetworkModSystem.GetConnectedNetworkAcross"/>.
  /// </summary>
  public static TNet? ConnectedNetworkAt<TNet>(
    this BlockEntity be,
    BlockPos at,
    BlockFacing connectorFace
  )
    where TNet : BlockNetwork =>
    be.NetworkSystem()
      ?.GetConnectedNetworkAcross(be.Api.World.BlockAccessor, at, connectorFace)
    as TNet;

  /// <summary>The network of type <typeparamref name="TNet"/> that owns <paramref name="pos"/>, or <c>null</c>.</summary>
  public static TNet? NetworkAt<TNet>(this BlockEntity be, BlockPos pos)
    where TNet : BlockNetwork => be.NetworkSystem()?.GetNetworkAt(pos) as TNet;
}
