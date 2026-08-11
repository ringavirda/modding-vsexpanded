using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Finds which network a cell belongs to. Vanilla's <see cref="BlockEntity.GetBehavior{T}"/> returns
/// the first behaviour of a CLR type, which cannot tell a pipe membership from a molten one on the
/// same block entity, so membership is selected by network type instead.
/// </summary>
public static class NetworkMembership {
  /// <summary>Every membership on <paramref name="be"/>; empty when it is on no network.</summary>
  public static IEnumerable<BEBehaviorNetworkMember> MembersOf(
    BlockEntity? be
  ) => be?.Behaviors.OfType<BEBehaviorNetworkMember>() ?? [];

  /// <summary>
  /// The membership declaring <paramref name="networkType"/>, or <c>null</c>. A block entity carries
  /// at most one membership per network type; two would be two nodes at one position. This matches
  /// the declared type, for a caller holding only a block entity; <see cref="Resolve"/> is the
  /// position-aware form and is what the graph walk asks.
  /// </summary>
  public static BEBehaviorNetworkMember? MemberOf(
    BlockEntity? be,
    string networkType
  ) =>
    MembersOf(be)
      .FirstOrDefault(m =>
        string.Equals(m.NetworkType, networkType, StringComparison.Ordinal)
      );

  /// <summary>
  /// How the cell at <paramref name="pos"/> participates in <paramref name="networkType"/>, or
  /// <c>null</c> when it does not. A membership behaviour answers first; otherwise the block does,
  /// which is what keeps a node in the graph while its chunk is unloaded and its block entity gone.
  /// </summary>
  public static INetworkMember? Resolve(
    IBlockAccessor world,
    BlockPos pos,
    string networkType
  ) {
    foreach (
      BEBehaviorNetworkMember member in MembersOf(world.GetBlockEntity(pos))
    )
      if (JoinsAt(member, world, pos, networkType))
        return member;

    return
      world.GetBlock(pos) is INetworkConnector connector
      && JoinsAt(connector, world, pos, networkType)
      ? connector
      : null;
  }

  /// <summary>
  /// Whether <paramref name="member"/> reports <paramref name="networkType"/> at
  /// <paramref name="pos"/>. Both arms of <see cref="Resolve"/> ask through this, so a member whose
  /// type varies by cell is found the same way whether a behaviour or a block answers.
  /// </summary>
  private static bool JoinsAt(
    INetworkMember member,
    IBlockAccessor world,
    BlockPos pos,
    string networkType
  ) =>
    string.Equals(
      member.NetworkTypeAt(world, pos),
      networkType,
      StringComparison.Ordinal
    );
}
