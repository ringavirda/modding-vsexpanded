using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no shipped layout pins the orientation of a network node. A node picks its own
/// orientation from its neighbours, so a pinned cell states a fact the node is free to contradict: the
/// structure can be left uncompletable, or a complete one broken when the player plumbs something
/// nearby. <c>MultiblockLayoutBuilder</c> refuses the multi-letter case outright; this is the half it
/// cannot judge, where <c>furnace-tuyere-n</c> reads exactly like <c>hopper-tall-e</c> and only the
/// referenced def says which is a node.
/// <para>
/// The sanctioned way to state what a layout wants is the <c>Connector</c> mark, which demands an
/// outward connector face rather than a variant.
/// </para>
/// </summary>
public static class PinnedNetworkNodes {
  /// <summary>
  /// Every pinned network node across <paramref name="sources"/>, as one line each, also reporting how
  /// many pinned codes were examined. Callers must assert that number is non-zero for the reason
  /// <see cref="MultiblockCodes"/> reports its own: a renamed attribute leaves the check passing while
  /// examining nothing. Pass every mod whose blocks the layouts may reference - a furnace layout pins
  /// parts from its own mod and from its dependencies alike.
  /// </summary>
  public static IReadOnlyList<string> Violations(
    out int codesChecked,
    params (string Domain, Assembly Assembly)[] sources
  ) {
    codesChecked = 0;

    // The network-oriented defs per domain, held as their expanded code shapes. Only nodes are kept:
    // this check has nothing to say about a pinned block the player orients.
    var nodesIn = new Dictionary<string, List<string[][]>>(
      StringComparer.Ordinal
    );
    var layouts = new List<(string Where, JObject Json)>();

    foreach ((string domain, Assembly asm) in sources) {
      if (!nodesIn.TryGetValue(domain, out var nodes))
        nodesIn[domain] = nodes = [];

      foreach (IExDef def in DefinitionGoldens.Collect(domain, asm)) {
        // A recipe def serialises as an array; indexing one by name throws rather than answering null.
        if (def.ToJson() is not JObject json)
          continue;
        layouts.Add((def.Location.ToString(), json));
        if (IsNode(json))
          nodes.Add(CodeShape(json));
      }
    }

    var problems = new List<string>();
    foreach ((string where, JObject json) in layouts) {
      if (json["attributes"]?["multiblockFacings"] is not JObject facings)
        continue;

      foreach (var entry in facings) {
        if (
          !MultiblockCodes.IsModDomainCode(
            entry.Key,
            out string domain,
            out string path
          )
        )
          continue;

        codesChecked++;
        if (
          nodesIn.TryGetValue(domain, out var nodes)
          && nodes.Any(shape => Matches(shape, path))
        )
          problems.Add(
            $"{where} pins '{entry.Key}', which is a network node - it re-picks its own orientation "
              + "from its neighbours, so the pin can be contradicted at any time. Mark the cell with "
              + "Connector instead."
          );
      }
    }
    return problems;
  }

  /// <summary>
  /// A def's code as a list of segment alternatives: the base code, then one entry per variant group
  /// holding that group's declared states. A group sourced from a world property declares none of its
  /// own, and stands in as <c>*</c> - unknown rather than empty, so a pin against it is not missed.
  /// </summary>
  /// <remarks>Matched shape by shape rather than by <c>def.Code</c>, which is the bare base code several
  /// defs share - every furnace part answers <c>furnace</c>, so asking which def provides
  /// <c>furnace-irontap-w</c> by base code alone names the tuyere too.</remarks>
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

  /// <summary>Whether a layout's (possibly wildcarded) code path names a variant of
  /// <paramref name="shape"/>: the same number of segments, each one an alternative that group declares
  /// or a wildcard on either side.</summary>
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

  /// <summary>Whether a def declares <c>ExOrientable</c> in <c>network</c> mode - the one thing that
  /// tells a node's single-letter code from a player-oriented block's.</summary>
  private static bool IsNode(JObject json) =>
    json["behaviors"] is JArray behaviors
    && behaviors
      .OfType<JObject>()
      .Any(b =>
        (string?)b["name"] == "ExOrientable"
        && (string?)b["properties"]?["mode"] == "network"
      );
}
