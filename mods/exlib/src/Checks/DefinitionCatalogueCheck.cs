using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Checks;

/// <summary>
/// Checks that every code-first block definition a domain declares actually produced a registered
/// block. Registering a def is not the same thing as the loader successfully turning it into a
/// block: a malformed variant group or a code the JSON-patch pipeline rewrote away leaves the def
/// registered and the block simply missing, with nothing else in the pipeline reporting it.
/// <para>
/// A def's own <c>code</c> plus its variant groups are expanded into patterns exactly as
/// <c>ExpandedLib.Testing.DefinitionCatalogue.BlockPatterns</c> does (a worldproperty-sourced group
/// becomes <c>*</c>, since it cannot be enumerated here either), then matched against
/// <see cref="ICheckSource.BlockCodes"/> - the concrete codes the game actually holds.
/// </para>
/// </summary>
public static class DefinitionCatalogueCheck {
  /// <summary>Every def in <paramref name="domain"/> that produced no registered block, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    AssetLocation[] registered = [.. source.BlockCodes];

    var errors = new List<string>();
    foreach (ExBlockDef def in source.BlockDefinitions(domain)) {
      bool resolves = Patterns(def)
        .Select(p => new AssetLocation(p))
        .Any(pattern => registered.Any(code => WildcardUtil.Match(pattern, code)));

      if (!resolves)
        errors.Add(
          $"{domain}:{def.Code} registers no block matching '{def.Domain}:{def.Code}*' - the "
            + "def is registered but the loader produced no block for it"
        );
    }
    return new CheckResult("DefinitionCatalogue", domain, errors);
  }

  // Every code pattern def.Code expands to once its variant groups are walked, a property group
  // standing in as a wildcard - the same shape ExpandedLib.Testing.DefinitionCatalogue.BlockPatterns
  // produces, kept independent here since this class must not depend on the test harness.
  private static IEnumerable<string> Patterns(ExBlockDef def) {
    var groups = new List<string[]>();
    if (def.ToJson()["variantgroups"] is JArray vg)
      foreach (JToken g in vg)
        groups.Add(
          g["states"] is JArray arr ? [.. arr.Select(s => (string)s!)] : ["*"]
        );

    IEnumerable<string> codes = [$"{def.Domain}:{def.Code}"];
    foreach (string[] states in groups)
      codes = codes.SelectMany(c => states.Select(s => c + "-" + s));
    return codes;
  }
}
