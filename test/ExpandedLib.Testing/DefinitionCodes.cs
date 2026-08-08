using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Expands code-first block definitions into the concrete block codes they register, so a test can
/// stand up a world that holds what the mods <b>actually</b> ship rather than blocks it invented.
/// <para>
/// <b>Why this exists.</b> A migration test that fabricates its own world proves nothing about
/// migration: <c>SmexToIwexMigrationTests</c> built <c>iwex:blastfurnace-tuyere-n</c> and
/// <c>iwex:blastfurnacetap-north</c>, neither of which any definition produces, and stayed green for
/// months while both of those released codes had no path to a live block at all. Expanding the real
/// definitions is the same trick the goldens and <see cref="BlockCodeEmitter"/> use - run the
/// definitions, do not restate them.
/// </para>
/// </summary>
public static class DefinitionCodes
{
  /// <summary>
  /// States for a variant group the <i>game</i> owns rather than our definitions.
  /// <para>
  /// Sampled, not enumerated - the states live in vanilla's assets and there is no registry
  /// headless. <c>horizontalorientation</c> is exact (it is a fixed four); the rest take one
  /// representative, which is enough to prove a code has a migration path. A state missing from
  /// <i>inside</i> a property group is a different and much rarer failure, and the per-migration
  /// tests already pin per-variant behaviour.
  /// </para>
  /// Kept identical to <c>scripts/gen-released-codes.py</c>'s sampling, so the released manifest
  /// and the live registry are expanded by one rule rather than two that can drift.
  /// </summary>
  private static readonly Dictionary<string, string[]> PropertySamples = new()
  {
    ["horizontalorientation"] = ["north", "east", "south", "west"],
    ["rockwithdeposit"] = ["granite"],
    ["rock"] = ["granite"],
  };

  /// <summary>
  /// One concrete registered block: its domain-qualified code and the variant map that produced it.
  /// <para>
  /// The variants are not decoration. Migrations read them - <c>PipeMigration</c> gates on
  /// <c>block.Variant["type"]</c> and would skip every pipe in a world of code-only blocks, silently
  /// turning the assertion that depends on it green.
  /// </para>
  /// </summary>
  public sealed record Registered(string Code, (string Key, string Value)[] Variants);

  /// <summary>Every concrete block <paramref name="def"/> registers, with its variant map.</summary>
  /// <param name="propertyGroupsAsWildcard">
  /// Renders a worldproperty-sourced group as a literal <c>*</c> instead of its
  /// <see cref="PropertySamples"/> representative, turning the result into a <b>pattern</b> to match
  /// against rather than a concrete code.
  /// <para>
  /// Use it whenever the question is "does this code name a registered block?" rather than "which
  /// blocks exist?". The samples are one state out of the game's dozens, so a caller comparing by
  /// equality would reject <c>molten-canal-moldpedestal-<b>basalt</b>-s</c> as unregistered purely
  /// because the sample happens to be granite - a false alarm about a real block.
  /// </para>
  /// </param>
  public static IEnumerable<Registered> Expand(
    ExBlockDef def,
    bool propertyGroupsAsWildcard = false
  )
  {
    var groups = new List<(string Name, string[] States)>();
    if (def.ToJson()["variantgroups"] is JArray vg)
      foreach (JToken g in vg)
      {
        string? props = (string?)g["loadFromProperties"];
        // A codeless worldproperty group takes its name from the property's last segment - vanilla's
        // form for horizontal orientation, and the same rule BlockCodeEmitter.GroupsOf applies.
        string name = (string?)g["code"] ?? props?.Split('/').Last() ?? "";
        if (name.Length == 0)
          continue;

        string[] states = g["states"] is JArray arr
          ? arr.Select(s => (string)s!).ToArray()
          : propertyGroupsAsWildcard
            ? ["*"]
            : PropertySamples.GetValueOrDefault(
              (props ?? "").Split('/').Last(),
              ["north"]
            );
        groups.Add((name, states));
      }

    string prefix = $"{def.Domain}:{def.Code}";
    IEnumerable<Registered> codes = [new Registered(prefix, [])];
    foreach (var (name, states) in groups)
      codes = codes.SelectMany(c =>
        states.Select(s => new Registered(
          c.Code + "-" + s,
          [.. c.Variants, (name, s)]
        ))
      );
    return codes;
  }

  /// <summary>Every concrete block <paramref name="domain"/> registers, from its own assembly.</summary>
  public static IEnumerable<Registered> ForDomain(string domain, Assembly asm) =>
    DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExBlockDef>()
      .SelectMany(d => Expand(d))
      .DistinctBy(r => r.Code);

  /// <summary>
  /// The same expansion as <see cref="ForDomain"/> but with worldproperty groups left as <c>*</c>, for
  /// a caller that needs to <b>test membership</b> of a code rather than enumerate the registry. Match
  /// with <c>WildcardUtil</c>; see <see cref="Expand"/>'s parameter for why equality is wrong here.
  /// </summary>
  public static IEnumerable<string> PatternsForDomain(string domain, Assembly asm) =>
    DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExBlockDef>()
      .SelectMany(d => Expand(d, propertyGroupsAsWildcard: true))
      .Select(r => r.Code)
      .Distinct();
}
