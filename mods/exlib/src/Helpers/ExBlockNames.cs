using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Helpers;

/// <summary>
/// Composes block display names that include the block's material variant: metal ("Piping
/// (Straight, Steel)"), canal rock, passthrough brick and any variant group a dependent mod has
/// registered through <see cref="AddVariantQualifier"/>, so that same-shaped blocks of different
/// materials are distinguishable in the inventory, handbook and look-at HUD. The qualifier is
/// always a parenthetical suffix, never a prefix: a leading noun does not decline to agree with
/// the block noun in Russian and Ukrainian.
/// </summary>
public static class ExBlockNames {
  // Ordered so registration order controls the order qualifiers apply in; a duplicate group name
  // replaces the prefix in place rather than moving it to the end (see AddVariantQualifier).
  private static readonly List<(string Group, string LangPrefix)> _qualifiers =
  [];

  /// <summary>
  /// Registers a variant group so <see cref="Decorate"/> also qualifies on it: a block whose
  /// <c>block.Variant[variantGroup]</c> is non-null gets a further parenthetical clause resolved
  /// through <c>Lang.Get(langPrefix + value)</c>. Applied after the built-in material/rock/brick
  /// clause, and after every other group registered earlier - registration order is application
  /// order. Registering a <paramref name="variantGroup"/> that is already registered replaces its
  /// <paramref name="langPrefix"/> without changing its position in that order.
  /// </summary>
  /// <param name="variantGroup">The block variant key to test (e.g. <c>"refractory"</c>).</param>
  /// <param name="langPrefix">Prepended to the variant value to form the lang key looked up
  /// (e.g. <c>"exlib:refractory-"</c> for value <c>"tier1"</c> resolves
  /// <c>"exlib:refractory-tier1"</c>).</param>
  public static void AddVariantQualifier(string variantGroup, string langPrefix) {
    for (int i = 0; i < _qualifiers.Count; i++) {
      if (_qualifiers[i].Group == variantGroup) {
        _qualifiers[i] = (variantGroup, langPrefix);
        return;
      }
    }
    _qualifiers.Add((variantGroup, langPrefix));
  }

  /// <summary>
  /// Decorates <paramref name="baseName"/> with the recognised variant values of
  /// <paramref name="block"/>. Metal materials and rocks resolve through the vanilla
  /// <c>material-*</c> and <c>rock-*</c> lang keys, brick variants through
  /// <c>{domain}:brickname-*</c> keys shipped by the block's own mod, then every group added
  /// through <see cref="AddVariantQualifier"/> in registration order. Blocks with none of these
  /// variants are returned unchanged.
  /// </summary>
  public static string Decorate(Block block, string baseName) {
    string name = baseName;

    string? material = block.Variant["material"];
    string? rock = block.Variant["rock"];
    string? brick = block.Variant["brick"];

    if (material != null)
      name = AppendQualifier(name, Lang.Get("material-" + material));
    else if (rock != null)
      name = AppendQualifier(name, Lang.Get("rock-" + rock));
    else if (brick != null)
      name = AppendQualifier(
        name,
        Lang.Get(block.Code.Domain + ":brickname-" + brick)
      );

    foreach ((string group, string langPrefix) in _qualifiers) {
      string? value = block.Variant[group];
      if (value != null)
        name = AppendQualifier(name, Lang.Get(langPrefix + value));
    }

    return name;
  }

  /// <summary>
  /// Appends <paramref name="qualifier"/> to <paramref name="name"/> as a parenthetical suffix. When
  /// the name already ends in a "(...)" group ("Piping (Straight)"), the qualifier is merged into that
  /// group ("Piping (Straight, Steel)") so brackets never stack.
  /// </summary>
  private static string AppendQualifier(string name, string qualifier) {
    if (name.EndsWith(')') && name.Contains('('))
      return name[..^1] + Lang.Get("exlib:blockname-listsep") + qualifier + ")";
    return Lang.Get("exlib:blockname-suffixed", name, qualifier);
  }
}
