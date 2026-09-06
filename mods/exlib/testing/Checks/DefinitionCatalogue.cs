using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Answers whether a code names something a mod registers, over blocks and items together, because
/// most callers hold a <c>JsonItemStack</c> whose <c>type</c> decides which registry it lands in. A
/// code that names nothing neither throws nor logs: the stack resolves to null and whatever depended
/// on it produces nothing. <see cref="RecipeCodes"/> covers the grid-recipe half of the same question.
/// <para>
/// Membership is tested with <see cref="WildcardUtil"/>, never equality. A worldproperty-sourced
/// variant group cannot be enumerated headlessly, so those groups expand to <c>*</c> and a concrete
/// code has to be matched against the pattern - see <see cref="DefinitionCodes.Expand"/>.
/// </para>
/// </summary>
public static class DefinitionCatalogue {
  /// <summary>Every block code pattern <paramref name="domain"/> registers, worldproperty groups as
  /// <c>*</c>.</summary>
  public static IEnumerable<string> BlockPatterns(
    string domain,
    Assembly asm
  ) => DefinitionCodes.PatternsForDomain(domain, asm);

  /// <summary>
  /// Every item code pattern <paramref name="domain"/> registers. Items carry no worldproperty groups
  /// in this codebase, so every group is enumerated exactly; the results are still called patterns
  /// because a caller matches them the same way for either registry.
  /// </summary>
  public static IEnumerable<string> ItemPatterns(string domain, Assembly asm) =>
    DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExItemDef>()
      .SelectMany(ExpandItem)
      .Distinct();

  private static IEnumerable<string> ExpandItem(ExItemDef def) {
    var groups = new List<string[]>();
    if (def.ToJson()["variantgroups"] is JArray vg)
      foreach (JToken g in vg)
        if (g["states"] is JArray arr)
          groups.Add([.. arr.Select(s => (string)s!)]);
        else
          // A worldproperty group on an item cannot be enumerated headlessly, so it becomes a
          // wildcard, as blocks' do. Dropping it would make every code under it unresolvable.
          groups.Add(["*"]);

    IEnumerable<string> codes = [$"{def.Domain}:{def.Code}"];
    foreach (string[] states in groups)
      codes = codes.SelectMany(c => states.Select(s => c + "-" + s));
    return codes;
  }

  /// <summary>
  /// Whether <paramref name="stack"/>'s code names something <paramref name="domains"/> register.
  /// <para>
  /// A stack whose domain is not among <paramref name="domains"/> (<c>game:</c>, or another mod's) is
  /// reported as resolvable, since the harness cannot see those registries.
  /// </para>
  /// </summary>
  public static bool Resolves(
    JsonItemStack? stack,
    IReadOnlyDictionary<string, Assembly> domains
  ) {
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
  public static bool Resolves(
    JsonItemStack? stack,
    string domain,
    Assembly asm
  ) => Resolves(stack, new Dictionary<string, Assembly> { [domain] = asm });
}
