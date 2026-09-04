using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Collects every code a mod's recipes, construction stages and definition bodies point at - as opposed
/// to the codes they register - and reports the ones that name nothing. A reference that resolves to
/// nothing never throws: a recipe with a dead
/// ingredient silently stops matching, and <c>ExConstruction</c> returns false on an unresolvable
/// non-wildcard require, so the structure cannot be built in survival or creative.
/// <para>
/// The point of collecting across mods rather than per mod is that a reference may cross a domain
/// boundary - a siex construction stage requiring an iiex pipe - and a per-mod suite cannot see the other
/// mod's registry. Resolve with <see cref="Unresolvable"/> from a suite that references all three.
/// </para>
/// </summary>
public static class ReferencedCodes {
  /// <summary>Where a reference was authored, which is what a failure message has to name for the
  /// reference to be findable.</summary>
  public enum Origin {
    /// <summary>A recipe's <c>output</c> - the thing the recipe produces.</summary>
    RecipeOutput,

    /// <summary>A recipe ingredient, from any of the three shapes (grid slot map, smithing singular,
    /// barrel array).</summary>
    RecipeIngredient,

    /// <summary>One <c>requireStacks</c> entry of an <c>ExRightClickConstructable</c> stage.</summary>
    ConstructionRequire,

    /// <summary>A stack a blocktype or itemtype names in its own body - a drop, a smelted or ground
    /// or shattered stack, a mold's output. Collected by shape rather than by key, so an attribute
    /// added later is covered without touching this.</summary>
    DefinitionStack,
  }

  /// <summary>One authored reference, with its placeholders already filled.</summary>
  /// <param name="Source">The def that authored it - a recipe's asset path, or a block code.</param>
  /// <param name="Code">The concrete (or wildcard) code, after placeholder expansion.</param>
  /// <param name="IsBlock">Which registry it lands in; <c>type</c> decides, defaulting to item as the
  /// game's own deserializer does.</param>
  public sealed record Reference(
    string Source,
    Origin Origin,
    string Code,
    bool IsBlock
  ) {
    /// <summary>The domain segment of <see cref="Code"/>, or <c>game</c> when it carries none - an
    /// unqualified code is a vanilla one, which is how <c>AssetLocation</c> parses it.</summary>
    public string Domain =>
      Code.Contains(':', StringComparison.Ordinal)
        ? Code[..Code.IndexOf(':', StringComparison.Ordinal)]
        : GlobalConstants.DefaultDomain;

    /// <inheritdoc/>
    public override string ToString() =>
      $"{Source}: {Code} ({(IsBlock ? "block" : "item")}, {Origin})";
  }

  #region Collecting

  /// <summary>Every code <paramref name="domain"/>'s recipe files reference - outputs and ingredients of
  /// all three recipe shapes.</summary>
  public static IEnumerable<Reference> InRecipes(string domain, Assembly asm) {
    foreach (
      ExRecipeDef def in DefinitionGoldens
        .Collect(domain, asm)
        .OfType<ExRecipeDef>()
    ) {
      string source = def.Location.ToShortString();
      JToken json = def.ToJson();
      foreach (JToken recipe in json is JArray arr ? arr : [json]) {
        Dictionary<string, string[]> holes = RecipeHoles(recipe);

        foreach (
          Reference r in Read(
            recipe["output"],
            source,
            Origin.RecipeOutput,
            holes
          )
        )
          yield return r;

        // Grid recipes key ingredients by pattern letter, barrel recipes list them, and smithing has
        // exactly one under the singular key. All three carry the same ingredient object.
        IEnumerable<JToken> ingredients = recipe["ingredients"] switch {
          JObject slots => slots.Properties().Select(p => p.Value),
          JArray list => list,
          _ => [],
        };
        foreach (JToken ingredient in ingredients.Append(recipe["ingredient"]))
          foreach (
            Reference r in Read(
              ingredient,
              source,
              Origin.RecipeIngredient,
              holes
            )
          )
            yield return r;
      }
    }
  }

  /// <summary>
  /// Every code <paramref name="domain"/>'s blocktypes and itemtypes name in their own bodies -
  /// construction requires, drops, smelted and ground and shattered stacks, mold outputs.
  /// <para>
  /// Found by shape, not by key: any object carrying a <c>code</c> string beside a <c>type</c> of
  /// <c>item</c> or <c>block</c> is a stack, and nothing else in a definition has that pair. Keying on
  /// the attribute names instead would silently stop covering an attribute the moment one is added,
  /// which is the failure this whole check exists to catch.
  /// </para>
  /// </summary>
  public static IEnumerable<Reference> InDefinitions(
    string domain,
    Assembly asm
  ) {
    foreach (IExDef def in DefinitionGoldens.Collect(domain, asm)) {
      if (def is not (ExBlockDef or ExItemDef))
        continue;

      JToken json = def.ToJson();
      string source = def.Location.ToShortString();
      Dictionary<string, string[]> holes = VariantStates(json);
      foreach ((string name, string[] states) in ConstructionWildCards(json))
        holes[name] = states;

      foreach (Reference r in Stacks(json, source, holes, false))
        yield return r;
    }
  }

  // Depth-first over a definition body. `inRequire` tracks whether the subtree sits under a
  // requireStacks array, which is the only thing that distinguishes a construction cost from any
  // other stack once the shape test has matched.
  private static IEnumerable<Reference> Stacks(
    JToken node,
    string source,
    Dictionary<string, string[]> holes,
    bool inRequire
  ) {
    if (node is JObject obj) {
      foreach (
        Reference r in Read(
          obj,
          source,
          inRequire ? Origin.ConstructionRequire : Origin.DefinitionStack,
          holes
        )
      )
        yield return r;

      foreach (JProperty property in obj.Properties())
        foreach (
          Reference r in Stacks(
            property.Value,
            source,
            holes,
            inRequire || property.Name == "requireStacks"
          )
        )
          yield return r;
    } else if (node is JArray array) {
      foreach (JToken child in array)
        foreach (Reference r in Stacks(child, source, holes, inRequire))
          yield return r;
    }
  }

  // One stack object into references, one per state its placeholders can take. An object without both
  // halves of the stack shape is not a reference: a variant group and a filler behavior both carry a
  // `code`, and neither names a collectible. An unfilled hole is left written so it reports as
  // unresolvable rather than passing on a code nothing supplies.
  private static IEnumerable<Reference> Read(
    JToken? stack,
    string source,
    Origin origin,
    Dictionary<string, string[]> holes
  ) {
    if (stack is not JObject obj || (string?)obj["code"] is not { } code)
      return [];

    bool isBlock = (string?)obj["type"] == "block";
    if (!isBlock && (string?)obj["type"] != "item")
      return [];

    return Fill(code, holes)
      .Select(c => new Reference(source, origin, c, isBlock));
  }

  #endregion

  #region Placeholders

  /// <summary>
  /// The <c>{name}</c> holes a recipe's codes can carry, mapped to the states they may take. A recipe
  /// binds one on an ingredient (<c>"name": "metal", "allowedVariants": [...]</c>) and interpolates it
  /// into the output and into other ingredients, which is how one recipe covers every metal.
  /// </summary>
  private static Dictionary<string, string[]> RecipeHoles(JToken recipe) {
    var holes = new Dictionary<string, string[]>(StringComparer.Ordinal);
    IEnumerable<JToken> ingredients = recipe["ingredients"] switch {
      JObject slots => slots.Properties().Select(p => p.Value),
      JArray list => list,
      _ => [],
    };

    foreach (JToken? ingredient in ingredients.Append(recipe["ingredient"]))
      Bind(holes, ingredient, "name");
    return holes;
  }

  /// <summary>
  /// The states a definition's own variant groups can take, which is what fills a <c>{name}</c> hole in
  /// a drop or a mold output - the game substitutes the block instance's own variant. A group sourced
  /// from a world property cannot be enumerated headlessly and binds to <c>*</c>, matching
  /// <see cref="DefinitionCodes.Expand"/>.
  /// </summary>
  private static Dictionary<string, string[]> VariantStates(JToken definition) {
    var holes = new Dictionary<string, string[]>(StringComparer.Ordinal);
    if (definition["variantgroups"] is not JArray groups)
      return holes;

    foreach (JToken group in groups) {
      string? name =
        (string?)group["code"]
        ?? ((string?)group["loadFromProperties"])?.Split('/').Last();
      if (string.IsNullOrEmpty(name))
        continue;
      holes[name] = group["states"] is JArray states
        ? [.. states.Select(s => (string)s!)]
        : ["*"];
    }
    return holes;
  }

  /// <summary>
  /// The wildcards a definition's construction stages store for later stages to fill.
  /// <c>ExConstruction</c> carries <c>storeWildCard</c> forward from the stack the player actually
  /// spent, so a later <c>{metal}</c> takes whichever of the storing ingredient's
  /// <c>allowedVariants</c> that was. Every stage is read at once because the states are what matters
  /// here, not the order they become available in.
  /// </summary>
  private static Dictionary<string, string[]> ConstructionWildCards(
    JToken definition
  ) {
    var holes = new Dictionary<string, string[]>(StringComparer.Ordinal);
    if (
      definition["entityBehaviors"] is not JArray behaviors
      || behaviors.FirstOrDefault(b =>
        (string?)b["name"] == nameof(ExRightClickConstructable)
      )
        is not { } rcc
      || rcc["properties"]?["stages"] is not JArray stages
    )
      return holes;

    foreach (JToken stage in stages)
      if (stage["requireStacks"] is JArray required)
        foreach (JToken ingredient in required)
          Bind(holes, ingredient, "storeWildCard");
    return holes;
  }

  // Records the states one ingredient binds under the name it declares at nameKey. Repeats union, since
  // two ingredients may bind the same hole to different metals and either is reachable.
  private static void Bind(
    Dictionary<string, string[]> holes,
    JToken? ingredient,
    string nameKey
  ) {
    if (
      ingredient?[nameKey] is not { } name
      || ingredient["allowedVariants"] is not JArray states
    )
      return;

    string key = (string)name!;
    string[] declared = [.. states.Select(s => (string)s!)];
    holes[key] = holes.TryGetValue(key, out string[]? seen)
      ? [.. seen.Union(declared)]
      : declared;
  }

  // Every concrete code the holes expand this one to. A hole with no binding stays written, because a
  // code nothing supplies a state for names no collectible and has to be reported as such.
  private static IEnumerable<string> Fill(
    string code,
    Dictionary<string, string[]> holes
  ) {
    IEnumerable<string> codes = [code];
    foreach ((string name, string[] states) in holes) {
      string hole = "{" + name + "}";
      codes = codes.SelectMany(c =>
        c.Contains(hole, StringComparison.Ordinal)
          ? states.Select(s => c.Replace(hole, s, StringComparison.Ordinal))
          : [c]
      );
    }
    return codes;
  }

  #endregion

  #region Resolving

  /// <summary>
  /// The subset of <paramref name="references"/> that name nothing any of <paramref name="domains"/>
  /// registers. A reference into a domain outside the map (<c>game:</c>, or a third party's) is left
  /// alone: this harness holds no registry for it, so reporting it would be a guess.
  /// </summary>
  public static IReadOnlyList<Reference> Unresolvable(
    IEnumerable<Reference> references,
    IReadOnlyDictionary<string, Assembly> domains
  ) {
    var catalogue = new Catalogue(domains);
    return [.. references.Where(r => !catalogue.Resolves(r)).Distinct()];
  }

  /// <summary>How many of <paramref name="references"/> this harness can actually judge - the ones
  /// whose domain is in <paramref name="domains"/>. Zero means a check over them proves nothing, which
  /// is worth asserting alongside the check itself.</summary>
  public static int Checkable(
    IEnumerable<Reference> references,
    IReadOnlyDictionary<string, Assembly> domains
  ) => references.Count(r => domains.ContainsKey(r.Domain));

  /// <summary>
  /// References written with no domain whose exact path names something a mod registers. An
  /// unqualified code parses as <c>game:</c>, so one of these either means vanilla's collectible and is
  /// shadowed by ours, or means ours and reads as vanilla's; a definition should say which registry it
  /// means rather than leave it to the loader.
  /// <para>
  /// Wildcards are excluded, and the exclusion is the point rather than an oversight. The metal
  /// families emit <c>metalplate-*</c>, <c>rod-*</c> and <c>metalnailsandstrips-*</c> into the mods'
  /// own domains alongside vanilla's, so a bare wildcard is a net cast over both registries and
  /// judging it would need a vanilla manifest this harness does not hold.
  /// </para>
  /// </summary>
  public static IReadOnlyList<Reference> BareButOurs(
    IEnumerable<Reference> references,
    IReadOnlyDictionary<string, Assembly> domains
  ) {
    var catalogue = new Catalogue(domains);
    return
    [
      .. references
        .Where(r =>
          !r.Code.Contains(':', StringComparison.Ordinal)
          && !r.Code.Contains('*', StringComparison.Ordinal)
          && !r.Code.Contains("@(", StringComparison.Ordinal)
        )
        .Where(r =>
          domains.Keys.Any(d =>
            catalogue.Resolves(r with { Code = $"{d}:{r.Code}" })
          )
        )
        .Distinct(),
    ];
  }

  // Registered code patterns per domain, expanded once. DefinitionGoldens.Collect rescans the assembly
  // and re-reads the metals catalogue off disk on every call, so a per-reference lookup without this
  // turns a second-long check into a minute-long one.
  private sealed class Catalogue(IReadOnlyDictionary<string, Assembly> domains) {
    private readonly Dictionary<
      (string Domain, bool IsBlock),
      AssetLocation[]
    > _cache = [];

    internal bool Resolves(Reference reference) {
      if (!domains.TryGetValue(reference.Domain, out Assembly? asm))
        return true;

      var target = new AssetLocation(reference.Code);
      // Both directions, because either side may carry a wildcard: a worldproperty-sourced variant
      // group cannot be enumerated headlessly and expands to `*` (see DefinitionCodes.Expand), while
      // an ingredient is routinely authored as one (`iiex:gear-*`). WildcardUtil only reads the
      // pattern side, so matching one way would miss whichever wildcard sat on the other.
      return Patterns(reference.Domain, reference.IsBlock, asm)
        .Any(p =>
          WildcardUtil.Match(p, target) || WildcardUtil.Match(target, p)
        );
    }

    private AssetLocation[] Patterns(string domain, bool isBlock, Assembly asm) {
      if (_cache.TryGetValue((domain, isBlock), out AssetLocation[]? cached))
        return cached;

      AssetLocation[] patterns =
      [
        .. (
          isBlock
            ? DefinitionCatalogue.BlockPatterns(domain, asm)
            : DefinitionCatalogue.ItemPatterns(domain, asm)
        ).Select(c => new AssetLocation(c)),
      ];
      _cache[(domain, isBlock)] = patterns;
      return patterns;
    }
  }

  #endregion
}
