using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that every grid recipe's <b>block output</b> names a block the mod actually registers.
/// <para>
/// <b>Why this is its own check.</b> A recipe output is the one code in the suite that <i>must</i> be
/// exact - it is a concrete block, not a wildcard selector - and it is also the one nothing verified. An
/// output naming a dead code does not throw: the recipe simply never resolves, so the machine quietly
/// stops being craftable while every test stays green. The 2026-08-04 side respelling left four of them
/// behind (<c>iwex:ore-mixer-north</c> and the three transmission housings), and the same sweep produced
/// the mirror-image defect two lines away - <c>slag-brickstairs-up-<b>n</b>-free</c>, where a *global*
/// rewrite respelled a group that legitimately keeps the vanilla word form.
/// </para>
/// <para>
/// <b>Scoped to block outputs in the mod's own domain</b>, deliberately. Item outputs are checked by
/// nothing here (items have no shared expansion yet), and a <c>game:</c> output belongs to
/// <c>VanillaCodes</c>, whose foolproofing is a per-version manifest rather than our definitions.
/// </para>
/// </summary>
public static class RecipeCodes
{
  /// <summary>One output that names no registered block: the recipe file it sits in and the code.</summary>
  public sealed record Unresolvable(string RecipePath, string Code);

  /// <summary>
  /// Every concrete block code <paramref name="domain"/>'s grid recipes can output - the set of blocks a
  /// player can actually craft, with placeholders expanded.
  /// <para>
  /// Separate from <see cref="UnresolvableOutputs"/> because the two ask opposite questions. That one
  /// asks "does this output name a real block?"; this one asks "which real blocks are craftable?", which
  /// is what a coverage check over some other per-block catalogue needs.
  /// </para>
  /// </summary>
  public static IEnumerable<string> OutputBlockCodes(string domain, Assembly asm)
  {
    foreach (ExRecipeDef def in DefinitionGoldens.Collect(domain, asm).OfType<ExRecipeDef>())
    {
      JToken json = def.ToJson();
      IEnumerable<JToken> recipes = json is JArray arr ? arr : [json];
      foreach (JToken recipe in recipes)
      {
        if (recipe["output"] is not JObject output)
          continue;
        if ((string?)output["type"] != "block" || (string?)output["code"] is not { } code)
          continue;
        if (!code.StartsWith(domain + ":", System.StringComparison.Ordinal))
          continue;

        foreach (string concrete in Expand(code, Placeholders(recipe)))
          yield return concrete;
      }
    }
  }

  /// <summary>
  /// Every block output in <paramref name="domain"/>'s recipes that no definition in the same assembly
  /// produces. Empty is the passing state.
  /// </summary>
  public static IReadOnlyList<Unresolvable> UnresolvableOutputs(string domain, Assembly asm)
  {
    // Patterns, not codes: a worldproperty group (the canal's `rock`) has dozens of states the
    // headless harness cannot enumerate, so membership is a wildcard match rather than a set lookup.
    AssetLocation[] registered =
    [
      .. DefinitionCodes.PatternsForDomain(domain, asm).Select(c => new AssetLocation(c)),
    ];

    var bad = new List<Unresolvable>();
    foreach (ExRecipeDef def in DefinitionGoldens.Collect(domain, asm).OfType<ExRecipeDef>())
    {
      JToken json = def.ToJson();
      IEnumerable<JToken> recipes = json is JArray arr ? arr : [json];
      foreach (JToken recipe in recipes)
      {
        if (recipe["output"] is not JObject output)
          continue;
        if ((string?)output["type"] != "block" || (string?)output["code"] is not { } code)
          continue;
        if (!code.StartsWith(domain + ":", System.StringComparison.Ordinal))
          continue;

        foreach (string concrete in Expand(code, Placeholders(recipe)))
        {
          var target = new AssetLocation(concrete);
          if (!registered.Any(p => WildcardUtil.Match(p, target)))
            bad.Add(new Unresolvable(def.Location.ToShortString(), concrete));
        }
      }
    }
    return bad;
  }

  /// <summary>
  /// The <c>{name}</c> holes a recipe's output can carry, mapped to the states they may take.
  /// <para>
  /// They come from the <b>ingredients</b>: a grid recipe binds a named variant on an ingredient
  /// (<c>"name": "brick", "allowedVariants": [...]</c>) and the output interpolates it, which is what
  /// makes one recipe cover seven brick colours. An unbound hole therefore means the output names a
  /// variant no ingredient supplies - reported rather than skipped, via the empty-list branch below.
  /// </para>
  /// </summary>
  private static Dictionary<string, string[]> Placeholders(JToken recipe)
  {
    var holes = new Dictionary<string, string[]>(System.StringComparer.Ordinal);
    if (recipe["ingredients"] is not JObject ingredients)
      return holes;

    foreach (JProperty slot in ingredients.Properties())
    {
      if (slot.Value["name"] is not { } name || slot.Value["allowedVariants"] is not JArray states)
        continue;
      holes[(string)name!] = [.. states.Select(s => (string)s!)];
    }
    return holes;
  }

  /// <summary>Every concrete code <paramref name="code"/> expands to once its holes are filled.</summary>
  private static IEnumerable<string> Expand(string code, Dictionary<string, string[]> holes)
  {
    IEnumerable<string> codes = [code];
    foreach (var (name, states) in holes)
    {
      string hole = "{" + name + "}";
      codes = codes.SelectMany(c =>
        c.Contains(hole, System.StringComparison.Ordinal)
          ? states.Select(s => c.Replace(hole, s, System.StringComparison.Ordinal))
          : [c]
      );
    }
    return codes;
  }
}
