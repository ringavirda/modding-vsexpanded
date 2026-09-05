using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Repo-relative path resolution for the per-mod layout (<c>mods/&lt;mod&gt;/{src,tests,assets,docs}</c>).
/// Every repo path a test or guard needs - an asset tree, a mod folder, a docs folder - is resolved
/// here, so a layout change is one file's edit rather than a sweep across every test.
/// </summary>
public static class RepoPaths {
  /// <summary>The solution root, resolved the same way <see cref="DefinitionGoldens.RepoRoot"/> does
  /// (walks up from the test binary to the directory carrying <c>.sln</c>/<c>.slnx</c>/<c>.git</c>, or
  /// <see cref="DefinitionGoldens.RepoRootOverride"/>/<c>EXLIB_REPO_ROOT</c> outside a checkout).</summary>
  public static string Root => DefinitionGoldens.RepoRoot();

  // A domain packs into whichever mod's zip ships it: the three mods carry their own domain, and
  // iiex also carries the shared game-lang overlay (assets/game/ lives inside iiex's asset tree).
  private static readonly Dictionary<string, string> DomainToMod = new(
    StringComparer.OrdinalIgnoreCase
  ) {
    ["exlib"] = "exlib",
    ["iiex"] = "iiex",
    ["siex"] = "siex",
    ["game"] = "iiex",
  };

  /// <summary>The absolute path to <c>mods/&lt;id&gt;</c> (a mod's folder root: src/tests/assets/docs).</summary>
  public static string Mod(string id) => Path.Combine(Root, "mods", id);

  /// <summary>The absolute path to a domain's packaged asset tree, <c>mods/&lt;mod&gt;/assets/&lt;domain&gt;</c>.
  /// <c>exlib</c>/<c>iiex</c>/<c>siex</c> map to their own mod folder; <c>game</c> maps to iiex, the mod whose
  /// zip ships the shared vanilla-lang overlay.</summary>
  public static string Assets(string domain) {
    if (!DomainToMod.TryGetValue(domain, out string? mod))
      throw new ArgumentOutOfRangeException(
        nameof(domain),
        domain,
        $"Unknown asset domain; add it to {nameof(RepoPaths)}.{nameof(DomainToMod)}."
      );
    return Path.Combine(Mod(mod), "assets", domain);
  }

  /// <summary>The absolute path to <c>mods/&lt;modId&gt;/docs</c> (handbook, screenshots, moddb pages).</summary>
  public static string Docs(string modId) => Path.Combine(Mod(modId), "docs");

  /// <summary>Every packaged asset-domain directory under any mod - <c>mods/*/assets/*</c> - for guards that
  /// scan the whole shipped-asset surface (e.g. <c>DefinitionGoldens.EmittedFamilies</c>) rather than one
  /// domain at a time.</summary>
  public static IReadOnlyList<string> AllAssetTrees() {
    string modsRoot = Path.Combine(Root, "mods");
    if (!Directory.Exists(modsRoot))
      return [];

    return Directory.EnumerateDirectories(modsRoot)
      .Select(mod => Path.Combine(mod, "assets"))
      .Where(Directory.Exists)
      .SelectMany(Directory.EnumerateDirectories)
      .OrderBy(p => p, StringComparer.Ordinal)
      .ToList();
  }
}
