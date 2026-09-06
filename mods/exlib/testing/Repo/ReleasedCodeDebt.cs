using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Released block codes that reach no live block today - the migration debt of ppex 0.6.8 and smex
/// 0.9.8, recorded 2026-08-14 so the coverage guard still fails on anything new. It was invisible
/// until <see cref="ReleasedCodes"/> was refreshed off 0.6.4/0.9.4, which is why it accumulated.
/// <para>
/// Most of it is one failure repeated: a <c>side</c> group that moved to
/// <c>loadFromProperties: abstract/horizontalorientation</c> ships vanilla's full words where the
/// live definition renders letters. The rest are blocktypes 0.9.8 added outright. Owner questions
/// and the full reasoning are in docs/internal/plans/STATE.md, B25.
/// </para>
/// <para>
/// This list may only shrink: paying a row off means writing its migration and deleting it here.
/// The rows live in each mod's own test <c>ModuleInit</c>, registered through
/// <see cref="ReleasedHistory"/>; this type is the old entry point the coverage tests still read.
/// </para>
/// </summary>
public static class ReleasedCodeDebt {
  /// <summary>Released codes with no path to a live block, grouped by the blocktype they came from.</summary>
  public static IReadOnlyList<string> KnownUnmigrated =>
    [.. ReleasedHistory.AllDebt];
}
