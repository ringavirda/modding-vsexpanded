using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Checks;

/// <summary>
/// Checks that no shipped layout pins the orientation of a network node. A node picks its own
/// orientation from its neighbours, so a pinned cell states a fact the node is free to contradict:
/// the structure can be left uncompletable, or a complete one broken when the player plumbs
/// something nearby. The sanctioned way to state what a layout wants is the <c>Connector</c> mark,
/// which demands an outward connector face rather than a variant.
/// <para>
/// A node is identified by its own declared contract - <c>ExOrientable</c> in <c>network</c> mode -
/// rather than by which C# class it comes from, since <see cref="ICheckSource"/> hands back defs,
/// not types.
/// </para>
/// </summary>
public static class PinnedNetworkNodesCheck {
  /// <summary>Every pinned network node among <paramref name="domain"/>'s layouts, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    // The network-oriented defs across every domain the source covers, held as expanded code shapes -
    // a furnace layout may pin a part from a dependency's domain as readily as its own.
    var nodesByDomain = new Dictionary<string, List<string[][]>>(
      StringComparer.Ordinal
    );
    foreach (string d in source.Domains) {
      var nodes = nodesByDomain[d] = [];
      foreach (ExBlockDef def in source.BlockDefinitions(d)) {
        JObject json = (JObject)def.ToJson();
        if (IsNode(json))
          nodes.Add(CodeShape(json));
      }
    }

    var errors = new List<string>();
    foreach (ExBlockDef def in source.BlockDefinitions(domain)) {
      JObject json = (JObject)def.ToJson();
      if (json["attributes"]?["multiblockFacings"] is not JObject facings)
        continue;

      foreach (var entry in facings) {
        if (
          !MultiblockCodesCheck.IsModDomainCode(
            entry.Key,
            out string wantDomain,
            out string wantPath
          )
        )
          continue;

        if (
          nodesByDomain.TryGetValue(wantDomain, out var nodes)
          && nodes.Any(shape => Matches(shape, wantPath))
        )
          errors.Add(
            $"{domain}:{def.Code} pins '{entry.Key}', which is a network node - it re-picks its "
              + "own orientation from its neighbours, so the pin can be contradicted at any time. "
              + "Mark the cell with Connector instead."
          );
      }
    }
    return new CheckResult("PinnedNetworkNodes", domain, errors);
  }

  // A def's code as a list of segment alternatives: the base code, then one entry per variant group
  // holding that group's declared states (a worldproperty-sourced group stands in as "*").
  private static string[][] CodeShape(JObject json) {
    var shape = new List<string[]> { new[] { (string?)json["code"] ?? "" } };

    if (json["variantgroups"] is JArray groups)
      foreach (JToken group in groups)
        shape.Add(
          group["states"] is JArray states && states.Count > 0
            ? [.. states.Select(s => (string?)s ?? "*")]
            : ["*"]
        );

    return [.. shape];
  }

  // Whether a layout's (possibly wildcarded) code path names a variant of shape: the same number of
  // segments, each an alternative that group declares or a wildcard on either side.
  private static bool Matches(string[][] shape, string path) {
    string[] parts = path.Split('-');
    if (parts.Length != shape.Length)
      return false;

    for (int i = 0; i < parts.Length; i++)
      if (
        parts[i] != "*"
        && !shape[i].Contains("*")
        && !shape[i].Contains(parts[i])
      )
        return false;

    return true;
  }

  // Whether a def declares ExOrientable in network mode - the one thing that tells a node's
  // single-letter code from a player-oriented block's.
  private static bool IsNode(JObject json) =>
    json["behaviors"] is JArray behaviors
    && behaviors
      .OfType<JObject>()
      .Any(b =>
        (string?)b["name"] == "ExOrientable"
        && (string?)b["properties"]?["mode"] == "network"
      );
}
