using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Verify;

/// <summary>
/// Every concrete block/item code a domain declares, resolved from its <c>blocktypes/</c> and
/// <c>itemtypes/</c> JSON the same way <c>RegistryObjectTypeLoader</c> does for a
/// <c>variantgroups</c> entry carrying its states inline (<c>{ "code": "type", "states": [...] }</c>).
/// A group that instead names <c>loadFromProperties</c> (a shared <c>config/worldproperties/*.json</c>
/// entry) needs the loader's own <c>ICoreServerAPI</c>-bound world-property resolution this tool does
/// not have, so that type's codes are never enumerated - its base code is recorded as an
/// <see cref="UnresolvedPrefixes"/> entry instead, and any reference under that prefix is treated as
/// resolvable rather than risking a false error.
/// </summary>
public sealed class BlockItemCatalogue {
  /// <summary>domain -> every concrete <c>code-state-state</c> string it registers (no domain
  /// prefix), one set for blocks and one for items.</summary>
  public Dictionary<string, HashSet<string>> Blocks { get; } = new(StringComparer.Ordinal);

  /// <inheritdoc cref="Blocks"/>
  public Dictionary<string, HashSet<string>> Items { get; } = new(StringComparer.Ordinal);

  /// <summary>domain -> every base code whose <c>variantgroups</c> could not be fully expanded, so a
  /// reference under it is never reported as a dangling code.</summary>
  public Dictionary<string, HashSet<string>> UnresolvedPrefixes { get; } = new(StringComparer.Ordinal);

  /// <summary>Builds the catalogue for every domain <paramref name="store"/> holds.</summary>
  public static BlockItemCatalogue Build(AssetStore store) {
    var catalogue = new BlockItemCatalogue();
    foreach (string domain in store.Domains) {
      catalogue.Blocks[domain] = [];
      catalogue.Items[domain] = [];
      catalogue.UnresolvedPrefixes[domain] = [];

      foreach ((string _, JToken json) in store.Under(domain, "blocktypes/"))
        if (json is JObject obj)
          catalogue.Expand(domain, obj, catalogue.Blocks[domain]);

      foreach ((string _, JToken json) in store.Under(domain, "itemtypes/"))
        if (json is JObject obj)
          catalogue.Expand(domain, obj, catalogue.Items[domain]);
    }
    return catalogue;
  }

  private void Expand(string domain, JObject type, HashSet<string> into) {
    string? code = (string?)type["code"];
    if (string.IsNullOrEmpty(code) || (bool?)type["enabled"] == false)
      return;

    if (type["variantgroups"] is not JArray groups || groups.Count == 0) {
      into.Add(code!);
      return;
    }

    List<string> combos = [code!];
    foreach (JToken groupToken in groups) {
      if (groupToken is not JObject group)
        continue;
      if (group["states"] is JArray states) {
        string[] values = [.. states.Select(s => (string?)s).OfType<string>()];
        combos = [.. combos.SelectMany(c => values.Select(v => $"{c}-{v}"))];
      } else {
        // loadFromProperties or some other group shape this tool cannot resolve headlessly -
        // treat the whole type as an unresolved prefix rather than guess at its states.
        UnresolvedPrefixes[domain].Add(code!);
        return;
      }
    }

    var skip = new HashSet<string>(
      ((JArray?)type["skipVariants"])?.Select(v => (string?)v).OfType<string>() ?? [],
      StringComparer.Ordinal
    );
    var allow = ((JArray?)type["allowedVariants"])?.Select(v => (string?)v).OfType<string>().ToList();

    foreach (string combo in combos) {
      if (skip.Contains(combo))
        continue;
      if (allow is { Count: > 0 } && !allow.Contains(combo))
        continue;
      into.Add(combo);
    }
  }
}
