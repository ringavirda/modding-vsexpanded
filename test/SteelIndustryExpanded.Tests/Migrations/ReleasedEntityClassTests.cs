using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The other half of the migration contract. <see cref="ReleasedCodeCoverageTests"/> proves every
/// shipped block CODE still reaches a live block; this proves every shipped block-entity CLASS still
/// resolves to a live type.
/// <para>
/// They fail differently, which is why one cannot stand in for the other. A class string is stored per
/// block entity in the SAVE and appears in no definition, so <c>CodeRelocation</c> never touches it and
/// no golden covers it. When it does not resolve the game simply does not construct the block entity:
/// <c>BlockMigrationModSystem</c> then reads <c>oldState</c> off a null, and the block migrates with its
/// contents gone - a canal arrives empty, a boiler keeps its shell and loses its water. Nothing logs a
/// failure and every other guard stays green.
/// </para>
/// </summary>
public class ReleasedEntityClassTests {
  /// <summary>The mod assemblies a released class string may resolve into, by live domain.</summary>
  private static readonly (string Domain, Assembly Asm)[] Mods =
  [
    ("iiex", typeof(IronIndustryExpanded.IiexBlocks).Assembly),
    ("siex", typeof(SiexBlocks).Assembly),
  ];

  /// <summary>Every legacy key the mods register aliases for, gathered from their own tables.</summary>
  private static IReadOnlySet<string> AliasedKeys() =>
    new HashSet<string>(
      IronIndustryExpanded
        .IronIndustryExpandedModSystem.LegacyEntityClasses.Select(a => a.Key)
        .Concat(
          SteelIndustryExpandedModSystem.LegacyEntityClasses.Select(a => a.Key)
        ),
      StringComparer.Ordinal
    );

  /// <summary><c>{domain}.{TypeName}</c> for every block-entity type the mods still ship, which is the
  /// key <c>EntityRegistry</c> derives at registration. A released class matching one of these needs no
  /// alias: neither its domain nor its type name moved.</summary>
  private static IReadOnlySet<string> LiveKeys() =>
    new HashSet<string>(
      Mods.SelectMany(m =>
        m.Asm.GetTypes()
          .Where(t =>
            t.Name.StartsWith("BlockEntity", StringComparison.Ordinal)
          )
          .Select(t => $"{m.Domain}.{t.Name}")
      ),
      StringComparer.Ordinal
    );

  /// <summary>A released blocktype whose every code is recorded debt migrates nowhere, so its class
  /// cannot save state for a block that never arrives and is exempt.</summary>
  private static bool BlockIsAbandoned(ReleasedCodes.ShippedEntityClass row) {
    var debt = new HashSet<string>(
      ReleasedCodeDebt.KnownUnmigrated,
      StringComparer.Ordinal
    );
    var codes = ReleasedCodes
      .All.Where(s =>
        s.Domain == row.Domain && row.AssetPaths.Contains(s.AssetPath)
      )
      .SelectMany(s => s.Codes)
      .ToList();
    return codes.Count > 0 && codes.All(debt.Contains);
  }

  [Fact]
  public void Every_released_entity_class_still_resolves_to_a_live_type() {
    var resolvable = new HashSet<string>(
      AliasedKeys().Concat(LiveKeys()),
      StringComparer.Ordinal
    );

    var orphans = ReleasedCodes
      .EntityClasses.Where(r => !resolvable.Contains(r.Class))
      .Where(r => !BlockIsAbandoned(r))
      .Select(r => $"{r.Class} (from {string.Join(", ", r.AssetPaths)})")
      .OrderBy(s => s, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      orphans.Count == 0,
      $"{orphans.Count} shipped block-entity class(es) resolve to nothing. Each one is a placed "
        + "block whose contents are dropped on load, with no error anywhere - register an alias in "
        + "the owning mod's LegacyEntityClasses:\n  "
        + string.Join("\n  ", orphans)
    );
  }

  [Fact]
  public void No_alias_points_at_a_class_that_never_shipped() {
    // The list may only shrink as content retires, never grow speculatively: an alias for a string no
    // release ever wrote is dead weight that reads as evidence the case was handled.
    var shipped = new HashSet<string>(
      ReleasedCodes.EntityClasses.Select(r => r.Class),
      StringComparer.Ordinal
    );
    // Development-only domains never reached a release, so their keys are exempt by construction.
    string[] devDomains = ["iwex.", "lpex."];

    var unknown = AliasedKeys()
      .Where(k => !shipped.Contains(k))
      .Where(k =>
        !devDomains.Any(d => k.StartsWith(d, StringComparison.Ordinal))
      )
      .OrderBy(s => s, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      unknown.Count == 0,
      "alias(es) for class strings no release ever wrote: "
        + string.Join(", ", unknown)
    );
  }
}
