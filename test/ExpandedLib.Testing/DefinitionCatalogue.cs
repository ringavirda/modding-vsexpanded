using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// "Does this code name something a mod actually registers?" - asked of <b>blocks and items together</b>,
/// because most of the places that need it hold a <c>JsonItemStack</c> whose <c>type</c> decides which
/// registry it lands in.
/// <para>
/// <b>Why this exists.</b> A code that names nothing does not throw and does not log: the stack simply
/// resolves to null and whatever depended on it silently produces nothing. A mold pattern whose output
/// names no item casts perfectly, hardens, and yields <i>nothing</i> on shake-out - the metal is gone and
/// the player has no way to tell that from a misrun. The same class of failure shipped an uncraftable ore
/// mixer, and <see cref="RecipeCodes"/> was written for the grid-recipe half of it.
/// </para>
/// <para>
/// Membership is tested with <see cref="WildcardUtil"/>, never equality. A worldproperty-sourced variant
/// group cannot be enumerated headlessly, so those groups expand to <c>*</c> and a concrete code has to be
/// <em>matched</em> against the pattern - see <see cref="DefinitionCodes.Expand"/>.
/// </para>
/// </summary>
public static class DefinitionCatalogue
{
  /// <summary>Every block code pattern <paramref name="domain"/> registers, worldproperty groups as
  /// <c>*</c>.</summary>
  public static IEnumerable<string> BlockPatterns(string domain, Assembly asm) =>
    DefinitionCodes.PatternsForDomain(domain, asm);

  /// <summary>
  /// Every item code pattern <paramref name="domain"/> registers. Items have no worldproperty groups in
  /// this codebase, so every group is enumerated exactly; the return type still says "pattern" because a
  /// caller must match it the same way either registry answers.
  /// </summary>
  public static IEnumerable<string> ItemPatterns(string domain, Assembly asm) =>
    DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExItemDef>()
      .SelectMany(ExpandItem)
      .Distinct();

  private static IEnumerable<string> ExpandItem(ExItemDef def)
  {
    var groups = new List<string[]>();
    if (def.ToJson()["variantgroups"] is JArray vg)
      foreach (JToken g in vg)
        if (g["states"] is JArray arr)
          groups.Add([.. arr.Select(s => (string)s!)]);
        else
          // A worldproperty group on an item: unenumerable headlessly, so it becomes a wildcard for the
          // same reason blocks' do. Silently dropping it would make every code under it "unresolvable".
          groups.Add(["*"]);

    IEnumerable<string> codes = [$"{def.Domain}:{def.Code}"];
    foreach (string[] states in groups)
      codes = codes.SelectMany(c => states.Select(s => c + "-" + s));
    return codes;
  }

  /// <summary>
  /// Whether <paramref name="stack"/>'s code names something <paramref name="domains"/> register.
  /// <para>
  /// A stack whose domain is not among <paramref name="domains"/> - <c>game:</c>, or another mod's -
  /// is reported as <b>resolvable</b>. This harness cannot see those registries, and answering "missing"
  /// for every vanilla output would make the check useless noise. It catches our own dangling codes,
  /// which is where the bug actually lives.
  /// </para>
  /// </summary>
  public static bool Resolves(
    JsonItemStack? stack,
    IReadOnlyDictionary<string, Assembly> domains
  )
  {
    if (stack?.Code is not { } code)
      return false;

    if (!domains.TryGetValue(code.Domain, out Assembly? asm))
      return true;

    IEnumerable<string> patterns =
      stack.Type == EnumItemClass.Block
        ? BlockPatterns(code.Domain, asm)
        : ItemPatterns(code.Domain, asm);

    return patterns.Any(p => WildcardUtil.Match(new AssetLocation(p), code));
  }

  /// <summary>Single-domain convenience for the common case.</summary>
  public static bool Resolves(JsonItemStack? stack, string domain, Assembly asm) =>
    Resolves(stack, new Dictionary<string, Assembly> { [domain] = asm });
}
