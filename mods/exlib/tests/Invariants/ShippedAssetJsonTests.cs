using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The per-mod JSON-defect rule (<see cref="ShippedJson"/>) over exlib's own tree, plus the guards
/// that stay whole-repo because they compare one domain's assets against another's: every shipped
/// domain is covered by the corpus (<see cref="ShippedJson"/> runs per mod, so nothing here checks
/// that on its own), every texture a domain names resolves somewhere in the repo, and no shipped
/// asset carries an authoring-machine path. The corpus for the whole-repo guards is derived from the
/// asset trees the build actually packs - every domain folder under <c>mods/*/assets/</c> and
/// <c>samples/*/assets/</c> - so a new mod or sample is covered on the day it is added and the
/// source-only <c>workbench/</c> tree stays out.
/// </summary>
public class ShippedAssetJsonTests {
  // Exlib has no patches/ folder of its own, so only the JSON-parses-and-carries-no-control-character
  // half of ShippedJson.Check applies here; the patch-side rule is exercised (with its premise) by
  // the mods that actually ship a patches/ folder - see mods/iiex, mods/siex.
  [Fact]
  public void Exlibs_own_tree_carries_no_shipped_json_defect() {
    IReadOnlyList<string> offenders = ShippedJson.Check(
      ExpandedLib.Testing.RepoPaths.Assets("exlib")
    );
    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_corpus_covers_every_shipped_domain() {
    // A path filter that silently matches nothing turns both theories above into no-ops, so the
    // premise is asserted rather than assumed.
    var covered = AssetFiles()
      .Select(p => p.Split('/')[3]) // mods/<mod>/assets/<domain>/...
      .Distinct()
      .ToHashSet(StringComparer.Ordinal);

    Assert.Equal(ShippedDomains().OrderBy(d => d), covered.OrderBy(d => d));
  }

  #region Corpus

  /// <summary>
  /// The asset domains the build packs: one folder under <c>mods/*/assets/</c> per shipped domain,
  /// including <c>game</c> where a mod's tree carries the shared vanilla lang overlay alongside its own.
  /// </summary>
  private static IEnumerable<string> ShippedDomains() {
    var domains = ExpandedLib.Testing.RepoPaths
      .AllAssetTrees()
      .Select(d => new DirectoryInfo(d).Name)
      .Distinct()
      .ToList();

    Assert.NotEmpty(domains);
    return domains;
  }

  /// <summary>
  /// Every JSON under a shipped domain, repo-relative and forward-slashed so the theory labels read
  /// the same on any platform.
  /// </summary>
  private static IEnumerable<string> AssetFiles() {
    string root = RepoRoot();
    foreach (string dir in ExpandedLib.Testing.RepoPaths.AllAssetTrees())
      foreach (
        string file in Directory.EnumerateFiles(
          dir,
          "*.json",
          SearchOption.AllDirectories
        )
      )
        yield return Path.GetRelativePath(root, file).Replace('\\', '/');
  }

  /// <summary>
  /// Every texture one of our own domains names, anywhere in a shipped JSON asset, must have a file
  /// behind it. Shapes are resolved by <c>DefinitionAssets.MissingShapes</c>; textures had no guard at
  /// all, and they are the more common half - a shape file carries a whole texture map, and a texture
  /// that does not resolve renders as the missing-texture checker with nothing in any log.
  /// </summary>
  [Fact]
  public void Every_texture_our_domains_name_resolves_to_a_file() {
    // `game` is a shipped domain here because iiex packs a vanilla lang override, but vanilla's
    // textures live in the game install rather than in this repository, so they are not resolvable.
    var domains = ShippedDomains().Where(d => d != "game").ToHashSet();
    var missing = new List<string>();

    foreach (string relative in AssetFiles()) {
      if (!relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        continue;
      foreach (
        Match m in TextureRef.Matches(
          File.ReadAllText(Path.Combine(RepoRoot(), relative))
        )
      ) {
        string domain = m.Groups["domain"].Value;
        if (!domains.Contains(domain))
          continue; // game: and other mods' textures live outside this repo

        string file = Path.Combine(
          ExpandedLib.Testing.RepoPaths.Assets(domain),
          "textures",
          m.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar)
            + ".png"
        );
        if (!File.Exists(file))
          missing.Add($"{relative}: '{m.Value}'");
      }
    }

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} texture reference(s) point where no file exists:\n  "
        + string.Join("\n  ", missing.Take(30))
        + (missing.Count > 30 ? $"\n  ... and {missing.Count - 30} more" : "")
    );
  }

  /// <summary>
  /// No shipped asset may carry an authoring-machine path. An editable shape names its textures by
  /// absolute path into the artist's own checkout; the export rewrites every one into a
  /// <c>domain:path</c> asset code, and a shape that still holds a raw path renders the missing-texture
  /// checker on someone else's machine with nothing in any log.
  /// </summary>
  /// <remarks>
  /// <see cref="Every_texture_our_domains_name_resolves_to_a_file"/> cannot see this: it matches
  /// <c>domain:path</c> references and skips anything whose domain is not ours, so an absolute path is
  /// invisible to it. The hole was found the hard way - <c>convert-shape.py</c> wrote its output before
  /// checking for unmapped textures, so a refused conversion silently replaced a correct shipped shape
  /// with the editable's <c>F:/...</c> paths, and every suite stayed green.
  /// </remarks>
  [Fact]
  public void No_shipped_asset_carries_an_authoring_path() {
    // A drive letter, and the two authoring-side trees. The drive letter is matched as exactly one
    // letter before the colon, so a URL scheme - the handbook's own `handbooksearch://` - cannot trip
    // it.
    var authoring = new Regex(
      @"\b[A-Za-z]:[\\/]|\.game/|workbench/",
      RegexOptions.Compiled
    );
    var offenders = new List<string>();

    foreach (string relative in AssetFiles()) {
      string text = File.ReadAllText(Path.Combine(RepoRoot(), relative));
      if (authoring.Match(text) is { Success: true } hit)
        offenders.Add($"{relative}: contains '{hit.Value}'");
    }

    Assert.True(
      offenders.Count == 0,
      $"{offenders.Count} shipped asset(s) carry an authoring path - re-export them:\n  "
        + string.Join("\n  ", offenders.Take(30))
    );
  }

  // A domain-qualified texture path as a JSON string value: "iiex:block/metal/castiron". Variant
  // placeholders are left to the shape guard's family matching; a path carrying one is skipped here.
  private static readonly Regex TextureRef = new(
    @"""(?<domain>[a-z]+):(?<path>block/[A-Za-z0-9_./-]+|item/[A-Za-z0-9_./-]+)""",
    RegexOptions.Compiled
  );

  private static string RepoRoot() {
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    Assert.True(dir != null, "could not locate repo root (VintageStory.sln)");
    return dir!.FullName;
  }

  #endregion
}
