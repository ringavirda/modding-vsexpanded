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

  // A domain packs into whichever mod's zip ships it: every mod the manifest names carries its own
  // domain, plus whatever overlay the manifest states (the shared game-lang overlay lives inside
  // iiex's asset tree, but is not iiex's own domain). Built once from RepoManifest, which exmod.json
  // does not change mid-process.
  private static readonly Dictionary<string, string> DomainToMod =
    BuildDomainToMod();

  private static Dictionary<string, string> BuildDomainToMod() {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (string modId in RepoManifest.Mods.Keys)
      map[modId] = modId;
    foreach ((string domain, string owner) in RepoManifest.Overlays)
      map[domain] = owner;
    return map;
  }

  /// <summary>Registers a domain's owning mod folder, for a domain a new mod ships that
  /// <see cref="DomainToMod"/> does not already know. A second call for the same domain replaces
  /// the first.</summary>
  public static void Register(string domain, string modFolder) =>
    DomainToMod[domain] = modFolder;

  /// <summary>The absolute path to a mod's or a sample's folder root (src/tests/assets/docs), resolved
  /// through <see cref="RepoManifest"/>: <paramref name="id"/> is looked up among
  /// <see cref="RepoManifest.Mods"/> first, then <see cref="RepoManifest.Samples"/> (a sample id resolves
  /// to its own project path, not its tests path), and otherwise falls back to <c>mods/&lt;id&gt;</c> - the
  /// default-layout convention, and what a brand-new id resolves to before it is registered anywhere.</summary>
  public static string Mod(string id) {
    if (RepoManifest.Mods.TryGetValue(id, out string? modPath))
      return modPath;
    if (
      RepoManifest.Samples.TryGetValue(id, out RepoManifest.SampleEntry sample)
    )
      return sample.Path;
    return Path.Combine(Root, "mods", id);
  }

  /// <summary>The absolute path to a domain's packaged asset tree, <c>&lt;mod or sample path&gt;/assets/&lt;domain&gt;</c>.
  /// A mod's own domain and a manifest overlay (e.g. <c>game</c> onto iiex) resolve through
  /// <see cref="DomainToMod"/>; an unknown domain falls back to <c>mods/&lt;domain&gt;</c> rather than
  /// throwing, so a new mod's own domain resolves before anyone calls <see cref="Register"/> for it.</summary>
  public static string Assets(string domain) {
    string mod = DomainToMod.TryGetValue(domain, out string? registered)
      ? registered
      : domain;
    return Path.Combine(Mod(mod), "assets", domain);
  }

  /// <summary>The absolute path to <c>mods/&lt;modId&gt;/docs</c> (handbook, screenshots, moddb pages).</summary>
  public static string Docs(string modId) => Path.Combine(Mod(modId), "docs");

  /// <summary>The absolute path to <paramref name="id"/>'s source folder: <c>&lt;mod path&gt;/src</c> when
  /// that directory exists, else the mod path itself. A sample keeps its sources under
  /// <c>&lt;path&gt;/src</c> the same way a mod does, so this resolves either.</summary>
  public static string Src(string id) {
    string modPath = Mod(id);
    string srcPath = Path.Combine(modPath, "src");
    return Directory.Exists(srcPath) ? srcPath : modPath;
  }

  /// <summary>Every packaged asset-domain directory under any mod or sample - <c>mods/*/assets/*</c> and
  /// <c>samples/*/assets/*</c> - for guards that scan the whole shipped-asset surface (e.g.
  /// <c>DefinitionGoldens.EmittedFamilies</c>) rather than one domain at a time.</summary>
  public static IReadOnlyList<string> AllAssetTrees() {
    IEnumerable<string> roots = RepoManifest.Mods.Values.Concat(
      RepoManifest.Samples.Values.Select(s => s.Path)
    );

    return roots
      .Select(root => Path.Combine(root, "assets"))
      .Where(Directory.Exists)
      .SelectMany(Directory.EnumerateDirectories)
      .OrderBy(p => p, StringComparer.Ordinal)
      .ToList();
  }
}
