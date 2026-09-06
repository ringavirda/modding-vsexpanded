using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Checks;

/// <summary>
/// The structural rules a definition must obey to end up on a network graph, all of which fail
/// silently in game: a network node must declare a <c>type</c> variant state and the orientation
/// scheme it actually ships, and a declared network membership must name the network it joins.
/// <para>
/// <c>ExpandedLib.Testing.NetworkNodeContract</c> selects a "node" definition by C# class
/// (<c>BlockNetworkNode</c>, <c>BEBehaviorNetworkMember</c> and their subclasses), which needs an
/// assembly to reflect over and so cannot run against <see cref="ICheckSource"/>. This class selects
/// the same definitions by the contract they declare instead: a node is one that declares
/// <c>ExOrientable</c> in <c>network</c> mode, and a membership is one declared under the framework's
/// own <c>BEBehaviorNetworkMember</c> key. Every shipped node and membership in this codebase already
/// satisfies both markers, so the two selections agree in practice; a mod that registered a network
/// membership under a differently-keyed subclass would be seen by the harness's reflective version
/// but not by this one - see <c>Checks.md</c>.
/// </para>
/// </summary>
public static class NetworkNodeContractCheck {
  // The bare registered key every network membership in this codebase declares itself under. The
  // harness's own NetworkNodeContract.MembershipViolations resolves this from BEBehaviorNetworkMember
  // and its subclasses by reflecting the assembly; this class has none to reflect, so it matches the
  // framework key literally instead.
  private const string MembershipKey = "BEBehaviorNetworkMember";

  /// <summary>Every contract violation among <paramref name="domain"/>'s network-node and membership defs, as the check's <see cref="CheckResult"/>.</summary>
  public static CheckResult Run(ICheckSource source, string domain) {
    var errors = new List<string>();
    foreach (ExBlockDef def in source.BlockDefinitions(domain)) {
      JObject json = (JObject)def.ToJson();

      if (IsNode(json)) {
        errors.AddRange(TypeGroupViolation(domain, def));
        errors.AddRange(SchemeViolation(domain, def, json));
      }

      errors.AddRange(MembershipViolations(domain, def, json));
    }
    return new CheckResult("NetworkNodeContract", domain, errors);
  }

  // Runtime AllowedOrientations comes from ExDefinitions.OrientationMap, which contributes nothing
  // for a def carrying no type states, so such a node gets an empty orientation map and TryPlaceBlock
  // refuses - with no exception and no log line.
  private static IEnumerable<string> TypeGroupViolation(string domain, ExBlockDef def) {
    if (def.VariantStates("type").Length > 0)
      yield break;

    yield return
      $"{domain}:{def.Code} declares NO `type` variant group - OrientationMap has nothing to "
        + "contribute, so AllowedOrientations is empty, ComputeValidOrientations returns [] and "
        + "TryPlaceBlock refuses. The block can never be placed, silently.";
  }

  // A node's orientation is picked by its neighbours, so the behaviour has to be told both that
  // (mode: "network") and which vocabulary the block writes (scheme, which cannot be derived from
  // the mode alone - a straight, a bend, a tee and a cross are all networks and declare different
  // sets). Both halves fail silently: a misspelled scheme falls back to ExOrientations.Axis, whose
  // three tokens reject every real token on a bend, and the node simply stops re-orienting.
  private static IEnumerable<string> SchemeViolation(
    string domain,
    ExBlockDef def,
    JObject json
  ) {
    string where = $"{def.Location} ({domain}:{def.Code})";

    JObject[] declared =
    [
      .. ArrayAt(json["behaviors"])
        .OfType<JObject>()
        .Where(b => (string?)b["name"] == "ExOrientable"),
    ];
    if (declared.Length != 1) {
      yield return
        $"{where} declares {declared.Length} `ExOrientable` behaviour(s), not 1 - a network node "
          + "takes its orientation from its neighbours, so it needs exactly one to write the "
          + "`orientation` variant through.";
      yield break;
    }

    string[] states = def.VariantStates("orientation");
    string? actual = ExOrientations.Resolve(states)?.Name;
    string? scheme = (string?)declared[0]["properties"]?["scheme"];

    if (actual == null) {
      yield return
        $"{where} declares orientation states [{string.Join(",", states)}], which set-equal no "
          + "scheme in ExOrientations.All - declare the scheme there rather than naming a near "
          + "match, whose rotation fallback would map onto a token this block does not have.";
      yield break;
    }

    if (scheme != actual)
      yield return
        $"{where} names scheme '{scheme ?? "<absent>"}' but its `orientation` states are "
          + $"{actual}'s - an unresolved name falls back to Axis, which rejects every token "
          + "outside [ns,we,ud] and stops the node re-orienting with no exception and no log line.";
  }

  // A membership created from a declaration starts with no network type of its own, and nothing
  // else can supply one: a footprint cell's block entity is a structure filler, and a plain block's
  // carries no type either. Such a cell logs an error and joins no graph.
  private static IEnumerable<string> MembershipViolations(
    string domain,
    ExBlockDef def,
    JObject json
  ) {
    foreach ((string where, JObject declaration) in MembershipDeclarations(json)) {
      string? networkType = (string?)declaration["properties"]?["networkType"];
      if (!string.IsNullOrEmpty(networkType))
        continue;

      yield return
        $"{domain}:{def.Code} declares a network membership in {where} with no `networkType` - "
          + "the behaviour has no other source for one, so the cell logs an error and joins no "
          + "graph. Give the declaration a networkType, or drop it.";
    }
  }

  private static IEnumerable<(string Where, JObject Declaration)> MembershipDeclarations(
    JObject json
  ) {
    foreach (JObject beh in ArrayAt(json["entityBehaviors"]).OfType<JObject>())
      if (BareKey((string?)beh["name"]) == MembershipKey)
        yield return ("entityBehaviors", beh);

    foreach (JToken table in FillerTables(json))
      foreach (JObject cell in ArrayAt(table).OfType<JObject>()) {
        string at = $"fillerOffsets cell ({cell["x"]},{cell["y"]},{cell["z"]})";
        foreach (JObject beh in ArrayAt(cell["behaviors"]).OfType<JObject>())
          if (BareKey((string?)beh["code"]) == MembershipKey)
            yield return (at, beh);
      }
  }

  private static IEnumerable<JToken> FillerTables(JObject json) {
    if (json["attributes"]?["fillerOffsets"] is JToken shared)
      yield return shared;

    if (json["attributesByType"] is JObject byType)
      foreach (JProperty variant in byType.Properties())
        if (variant.Value["fillerOffsets"] is JToken perType)
          yield return perType;
  }

  // The part of a declared behaviour key after its mod-id prefix.
  private static string BareKey(string? code) {
    string key = code ?? "";
    int dot = key.LastIndexOf('.');
    return dot < 0 ? key : key[(dot + 1)..];
  }

  // Whether a def declares ExOrientable in network mode - see the class remarks for why that marker
  // stands in for "is a BlockNetworkNode" here.
  private static bool IsNode(JObject json) =>
    ArrayAt(json["behaviors"])
      .OfType<JObject>()
      .Any(b =>
        (string?)b["name"] == "ExOrientable"
        && (string?)b["properties"]?["mode"] == "network"
      );

  private static IEnumerable<JToken> ArrayAt(JToken? token) =>
    token as JArray ?? Enumerable.Empty<JToken>();
}
