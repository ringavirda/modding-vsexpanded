using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Checks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Testing;

/// <summary>
/// An <see cref="ICheckSource"/> over this repository's source tree, for the same checks a mod's
/// xUnit suite runs to see against real committed assets rather than a stub. Reads exactly what the
/// harness validators in <c>Checks/</c> read today: code-first defs through <see cref="ExDefinitions"/>
/// (scanning each domain's assembly, resolved via its <c>[assembly: ExDomain]</c> declaration), and
/// lang straight off the tree through <see cref="RepoPaths"/> - the harness's checks never had a
/// code-first path for lang, since it is authored as plain JSON.
/// </summary>
public sealed class RepoCheckSource : ICheckSource {
  private readonly string[] _domains;
  private readonly Dictionary<string, Assembly> _assemblies;

  /// <param name="repoRoot">Passed through to <see cref="DefinitionGoldens.RepoRootOverride"/> so
  /// every path this source resolves is relative to it.</param>
  /// <param name="domains">The domains this source covers, e.g. <c>"exlib", "iiex", "siex"</c>. Each
  /// must have an already-loaded assembly declaring <c>[assembly: ExDomain(domain)]</c> - normally
  /// true simply by referencing that mod's project from the test project this runs in.</param>
  public RepoCheckSource(string repoRoot, params string[] domains) {
    DefinitionGoldens.RepoRootOverride = repoRoot;
    _domains = domains;
    _assemblies = ResolveAssemblies(domains);
  }

  /// <inheritdoc/>
  public IEnumerable<string> Domains => _domains;

  /// <inheritdoc/>
  public IEnumerable<AssetLocation> BlockCodes =>
    _domains.SelectMany(d =>
      DefinitionCodes
        .ForDomain(d, _assemblies[d])
        .Select(r => new AssetLocation(r.Code))
    );

  /// <inheritdoc/>
  public IEnumerable<AssetLocation> ItemCodes =>
    _domains.SelectMany(d =>
      DefinitionCatalogue
        .ItemPatterns(d, _assemblies[d])
        .Select(p => new AssetLocation(p))
    );

  /// <inheritdoc/>
  public IEnumerable<(AssetLocation File, JObject Json)> Recipes(string domain) {
    foreach (
      ExRecipeDef def in DefinitionGoldens
        .Collect(domain, _assemblies[domain])
        .OfType<ExRecipeDef>()
    ) {
      JToken json = def.ToJson();
      foreach (JToken recipe in json is JArray arr ? arr : [json])
        if (recipe is JObject obj)
          yield return (def.Location, obj);
    }
  }

  /// <inheritdoc/>
  public IEnumerable<(string Locale, JObject Json)> Lang(string domain) {
    string langDir = Path.Combine(RepoPaths.Assets(domain), "lang");
    if (!Directory.Exists(langDir))
      yield break;

    foreach (
      string file in Directory.EnumerateFiles(langDir, "*.json").OrderBy(f => f)
    )
      yield return (
        Path.GetFileNameWithoutExtension(file),
        JObject.Parse(File.ReadAllText(file))
      );
  }

  /// <inheritdoc/>
  public IEnumerable<ExBlockDef> BlockDefinitions(string domain) =>
    DefinitionGoldens.Collect(domain, _assemblies[domain]).OfType<ExBlockDef>();

  // Resolves each domain to the assembly declaring it, via [assembly: ExDomain(domain)] on whichever
  // already-loaded assembly carries it - normally true simply by referencing that mod's project from
  // the test project this source runs in.
  private static Dictionary<string, Assembly> ResolveAssemblies(
    string[] domains
  ) {
    Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
    var map = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    foreach (string domain in domains) {
      Assembly? found = loaded.FirstOrDefault(a =>
        a.GetCustomAttribute<ExDomainAttribute>()?.Domain == domain
      );
      map[domain] =
        found
        ?? throw new InvalidOperationException(
          $"No loaded assembly declares [assembly: ExDomain(\"{domain}\")] - reference that mod's "
            + "project from the test project constructing this RepoCheckSource."
        );
    }
    return map;
  }
}
