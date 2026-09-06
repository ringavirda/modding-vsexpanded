using System;

namespace ExpandedLib.Blocks;

/// <summary>
/// The lazy build behind every block entity's or block-entity-behaviour's <c>Persisted</c> property:
/// <see cref="ExBlockEntity"/>, <see cref="ExBlockEntityBehavior"/>, <see cref="ExBlockEntityContainer"/>
/// and the framework's other bases each own a nullable backing field and call
/// <see cref="GetOrCreate"/> from their getter rather than repeating the build sequence themselves.
/// </summary>
internal static class BlockEntityStateHost {
  /// <summary>
  /// Returns <paramref name="backing"/>, building it on first call: a fresh <see cref="ExBlockState"/>,
  /// scanned for <see cref="PersistAttribute"/> members, then handed to <paramref name="declareState"/>
  /// for whatever <paramref name="owner"/> declares by hand. <paramref name="backing"/> is assigned
  /// before either step runs, so a member whose accessor reads <c>Persisted</c> recurses into the same
  /// instance rather than looping forever. <paramref name="owner"/> is a block entity or a block-entity
  /// behaviour - <see cref="PersistScan"/> only ever reflects over its type.
  /// </summary>
  public static ExBlockState GetOrCreate(
    object owner,
    ref ExBlockState? backing,
    Action<ExBlockState> declareState
  ) {
    if (backing != null)
      return backing;
    var state = new ExBlockState();
    backing = state;
    PersistScan.Declare(owner, state);
    declareState(state);
    return state;
  }
}
