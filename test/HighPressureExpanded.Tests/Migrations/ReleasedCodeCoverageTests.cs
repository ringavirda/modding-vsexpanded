using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Blocks.Migrations;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// The migration contract: every block code that has ever shipped (<see cref="ReleasedCodes"/>,
/// extracted from <c>dist/Releases/</c>) must still reach a live block, or an explicit purge, through
/// the declared migrations. A code that fails this test is a block a player has built and will lose.
/// A code absent from <see cref="ReleasedCodes"/> never shipped and needs no migrator, which bounds a
/// rename to the shipped paths.
/// <para>
/// Migration chains cross mods (<c>ppex</c> to <c>lpex</c> to <c>hpex</c>, <c>smex</c> to <c>iwex</c>),
/// so the assertion only holds where every migrator is visible: <c>HighPressureExpanded.Tests</c> is the
/// only suite that references all five mods.
/// </para>
/// </summary>
public class ReleasedCodeCoverageTests {
  // One anchor per mod assembly. The generated code tables exist in every domain and are regenerated
  // from the definitions themselves.
  private static readonly (string Domain, Assembly Asm)[] Domains =
  [
    ("exlib", typeof(BlockMigrationModSystem).Assembly),
    ("iwex", typeof(IronworkingExpanded.IwexBlocks).Assembly),
    ("lpex", typeof(LowPressureExpanded.LpexBlocks).Assembly),
    ("hpex", typeof(HpexBlocks).Assembly),
    ("smex", typeof(SteelmakingExpanded.SmexBlocks).Assembly),
  ];

  /// <summary>Every block the five mods actually register, expanded from the real definitions.</summary>
  private static IReadOnlyList<DefinitionCodes.Registered> LiveBlocks() =>
    [.. Domains.SelectMany(d => DefinitionCodes.ForDomain(d.Domain, d.Asm))];

  /// <summary>
  /// A world holding exactly the live blocks, variant maps included. The migrators enumerate
  /// <c>World.Blocks</c> to derive their remaps, so this is what they see.
  /// </summary>
  private static TestWorld LiveWorld(
    IEnumerable<DefinitionCodes.Registered> registered
  ) {
    var world = new TestWorld();
    var blocks = new List<Block>();
    int id = 1000;
    foreach (DefinitionCodes.Registered r in registered) {
      Block b = TestBlocks.Configure(new Block(), r.Code, id++, r.Variants);
      world.Register(b);
      blocks.Add(b);
    }
    world.World.Blocks.Returns(blocks);
    return world;
  }

  /// <summary>Follows the declared remap graph from <paramref name="from"/> to its terminal code.</summary>
  private static string Terminal(
    string from,
    IReadOnlyDictionary<string, string> declared
  ) {
    string cursor = from;
    var seen = new HashSet<string> { cursor };
    while (declared.TryGetValue(cursor, out string? next) && seen.Add(next))
      cursor = next;
    return cursor;
  }

  #region The contract

  [Fact]
  public void Every_released_block_code_still_reaches_a_live_block() {
    IReadOnlyList<DefinitionCodes.Registered> registered = LiveBlocks();
    var live = registered.Select(r => r.Code).ToHashSet();
    TestWorld world = LiveWorld(registered);

    // First declaration wins, matching BlockMigrationModSystem's own conflict policy.
    var declared = new Dictionary<string, string>();
    foreach (var r in BlockMigrationModSystem.DeclaredBlockRemaps(world.Api))
      declared.TryAdd(r.OldCode.ToString(), r.NewCode.ToString());

    var purged = BlockMigrationModSystem
      .DeclaredRemovals(world.Api)
      .Select(r => r.Code.ToString())
      .ToHashSet();

    var orphans = new List<string>();
    foreach (ReleasedCodes.Shipped shipped in ReleasedCodes.All)
      foreach (string code in shipped.Codes) {
        // Still registered under its original code, so there is nothing to migrate.
        if (live.Contains(code))
          continue;

        string terminal = Terminal(code, declared);
        if (purged.Contains(terminal) || live.Contains(terminal))
          continue;

        orphans.Add(
          terminal == code
            ? $"{code}  ({shipped.AssetPath}) - no migration declares it at all"
            : $"{code}  ({shipped.AssetPath}) - chain ends at '{terminal}', which is not a live block"
        );
      }

    Assert.True(
      orphans.Count == 0,
      $"{orphans.Count} released block code(s) have no path to a live block. A player who built "
        + "one of these loses it silently - BuildRemapTable drops an unresolvable pair with only a "
        + "Logger.Warning.\n  "
        + string.Join("\n  ", orphans.Take(40))
        + (orphans.Count > 40 ? $"\n  ... and {orphans.Count - 40} more" : "")
    );
  }

  #endregion

  #region Guards on the manifest itself

  [Fact]
  public void The_released_manifest_covers_the_three_mods_that_shipped() {
    // An emptied manifest would make the contract above vacuous.
    Assert.Equal(18, ReleasedCodes.Ppex.Count);
    Assert.Equal(27, ReleasedCodes.Smex.Count);
    Assert.Single(ReleasedCodes.Exlib);
    Assert.All(ReleasedCodes.All, s => Assert.NotEmpty(s.Codes));
  }

  [Fact]
  public void No_migration_claims_a_code_that_is_still_alive() {
    // A migration's old code must be dead. Declaring a live one makes the migrator rewrite blocks that
    // players are legitimately using, and BuildRemapTable's only guard (GetBlock(oldCode) != null)
    // passes for a live block, so nothing downstream catches it.
    IReadOnlyList<DefinitionCodes.Registered> registered = LiveBlocks();
    var live = registered.Select(r => r.Code).ToHashSet();
    TestWorld world = LiveWorld(registered);

    var offenders = BlockMigrationModSystem
      .DeclaredBlockRemaps(world.Api)
      .Where(r => live.Contains(r.OldCode.ToString()))
      .Select(r => $"'{r.Migration}': {r.OldCode} (LIVE) → {r.NewCode}")
      .Distinct()
      .ToList();

    Assert.True(
      offenders.Count == 0,
      $"{offenders.Count} migration source(s) are still registered blocks. Each one silently rewrites "
        + "a block players are using:\n  "
        + string.Join("\n  ", offenders.Take(30))
        + (
          offenders.Count > 30 ? $"\n  ... and {offenders.Count - 30} more" : ""
        )
    );
  }

  [Fact]
  public void No_released_code_is_claimed_by_two_different_migrations() {
    TestWorld world = LiveWorld(LiveBlocks());

    var byOld = new Dictionary<string, (string Migration, string New)>();
    var conflicts = new List<string>();
    foreach (var r in BlockMigrationModSystem.DeclaredBlockRemaps(world.Api)) {
      string old = r.OldCode.ToString();
      if (byOld.TryGetValue(old, out var first)) {
        if (first.New != r.NewCode.ToString())
          conflicts.Add(
            $"{old}: '{first.Migration}' → {first.New}  vs  '{r.Migration}' → {r.NewCode}"
          );
        continue;
      }
      byOld[old] = (r.Migration, r.NewCode.ToString());
    }

    Assert.True(
      conflicts.Count == 0,
      "Two migrations disagree about where a code goes; the first-wins policy makes the loser "
        + "silently dead:\n  "
        + string.Join("\n  ", conflicts)
    );
  }

  #endregion
}
