using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Helpers;

/// <summary>
/// Composes block display names that include the block's material variant: pipe metal
/// ("Piping (Straight, Steel)"), canal rock, passthrough brick and refractory tier, so that
/// same-shaped blocks of different materials are distinguishable in the inventory, handbook and
/// look-at HUD. The qualifier is always a parenthetical suffix, never a prefix: a leading noun does
/// not decline to agree with the block noun in Russian and Ukrainian.
/// </summary>
public static class ExBlockNames {
  /// <summary>
  /// Decorates <paramref name="baseName"/> with the recognised variant values of
  /// <paramref name="block"/>. Metal materials and rocks resolve through the vanilla
  /// <c>material-*</c> and <c>rock-*</c> lang keys, brick variants through
  /// <c>{domain}:brickname-*</c> keys shipped by the block's own mod. Blocks with none of these
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

    // Refractory tier is its own variant group (cowper stove / smoke stack intakes).
    string? refractory = block.Variant["refractory"];
    if (refractory != null)
      name = AppendQualifier(name, Lang.Get("exlib:refractory-" + refractory));

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
