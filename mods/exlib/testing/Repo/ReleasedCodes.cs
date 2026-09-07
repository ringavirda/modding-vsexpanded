using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Every block code that has ever shipped, read from the release artifacts in <c>dist/Releases/</c>
/// rather than from the live registry. This is the migration contract: a code in this list must
/// still reach a live block after migration; a code absent from it never shipped and needs no
/// migrator.
/// <para>
/// Three mods have shipped: <c>exlib</c>, <c>ppex</c> (now iiex) and <c>smex</c>; no iiex, siex or
/// hpex build has been released. Rows accumulate and are never pruned - a variant grammar can change
/// between releases and both spellings are in player worlds. The rows themselves live in each mod's
/// own test <c>ModuleInit</c>, registered through <see cref="ReleasedHistory"/>; this type is the
/// old entry point they still read through.
/// </para>
/// </summary>
public static class ReleasedCodes {
  /// <summary>One shipped blocktype: where it lived, its base code, and every concrete code it
  /// expanded to. Property-sourced variant groups are sampled rather than enumerated (the game
  /// holds their states), so <see cref="Codes"/> is representative for those, exact otherwise.</summary>
  public sealed record Shipped(
    string Domain,
    string AssetPath,
    string BaseCode,
    string[] Codes
  );

  /// <summary>A block-entity class string a released blocktype declared, and the blocktypes that
  /// declared it.</summary>
  public sealed record ShippedEntityClass(
    string Domain,
    string Class,
    string[] AssetPaths
  );

  /// <summary>ppex, every release up to and including 0.6.8 - 19 blocktypes, 292 concrete codes.
  /// Registered by iiex's test <c>ModuleInit</c> (ppex became iiex).</summary>
  public static IReadOnlyList<Shipped> Ppex =>
    ReleasedHistory.For("iiex")?.Shipped ?? [];

  /// <summary>smex, every release up to and including 0.9.8 - 35 blocktypes, 351 concrete codes.
  /// Registered by siex's test <c>ModuleInit</c> (siex's coverage tests are the only ones that
  /// consume it at runtime).</summary>
  public static IReadOnlyList<Shipped> Smex =>
    ReleasedHistory.For("siex")?.Shipped ?? [];

  /// <summary>exlib 0.7.0 - no blocktype JSON, but the filler is written into released worlds by
  /// every mega-block footprint, so it carries the same migration contract as a placed block.
  /// Registered by exlib's test <c>ModuleInit</c>.</summary>
  public static IReadOnlyList<Shipped> Exlib =>
    ReleasedHistory.For("exlib")?.Shipped ?? [];

  /// <summary>Every block-entity class string a released blocktype declared, with the blocktypes
  /// that declared it. A class string lives in the SAVE and no code migration touches it, so an
  /// unregistered one drops the block entity: the block arrives, its contents do not.</summary>
  public static IReadOnlyList<ShippedEntityClass> EntityClasses =>
    [.. ReleasedHistory.AllEntityClasses];

  /// <summary>Every shipped blocktype across all three released mods.</summary>
  public static IEnumerable<Shipped> All => ReleasedHistory.AllShipped;

  /// <summary>Every concrete code that has ever been placed in a released world.</summary>
  public static IEnumerable<string> AllCodes => All.SelectMany(s => s.Codes);
}
