using System.Collections.Generic;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// What a reheat furnace's hearth can hold, and which shape elements draw it.
/// <para>
/// The bed is three rows wide and takes <b>one piece of stock per row</b>. That is the honest capacity:
/// the vanilla forge tops out at an ingot, so the reason this furnace exists at all is that a shingled
/// bloom or a cast slab will not fit in one - and a slab is two cells deep, which is why the hearth's
/// footprint is 3x2 rather than a strip.
/// </para>
/// <para>
/// Pure and tested against the shipped shape: every string here is aimed at art, and selective-element
/// matching drops an unknown name silently.
/// </para>
/// </summary>
public static class HeatingHearthLayout
{
  /// <summary>The stock forms the hearth has art for. One element per form per row.</summary>
  public enum Stock
  {
    ShingledBloom,
    ShingledSlab,
    CastBillet,
    CastBloom,
    CastSlab,
  }

  /// <summary>Rows on the bed - one piece each.</summary>
  public const int Rows = 3;

  // The element base names, in the order the shape declares them inside each Items group. The suffix
  // differs per group (none / "2" / "3"), which is handled below.
  private static readonly Dictionary<Stock, string> _base = new()
  {
    [Stock.ShingledBloom] = "ShingledBlooms",
    [Stock.ShingledSlab] = "ShingledSlabs",
    [Stock.CastBillet] = "CastBillets",
    [Stock.CastBloom] = "CastBlooms",
    [Stock.CastSlab] = "CastSlabs",
  };

  // Note: the Items groups are not in positional order: their children are byte-identical, so the group's own
  // `from` is the translation that decides where each draws. Verified against the shipped shape -
  // Items1 at x0 (left cell), Items3 at x16 (centre), Items2 at x32 (right). Reading "Items2 = middle"
  // off the name puts every loaded piece one cell out, and nothing throws.
  // The children carry the same suffix as their group, so Items3's blooms are "ShingledBlooms3".
  private static readonly Dictionary<HearthRows.Row, string> _suffix = new()
  {
    [HearthRows.Row.Left] = "",
    [HearthRows.Row.Centre] = "3",
    [HearthRows.Row.Right] = "2",
  };

  /// <summary>The element path drawing <paramref name="stock"/> lying in <paramref name="row"/>.</summary>
  public static string Element(HearthRows.Row row, Stock stock) =>
    HearthRows.ElementGroup(row) + "/" + _base[stock] + _suffix[row] + "/*";

  /// <summary>
  /// Maps a stock item's code to the form the hearth draws, or null when the item is not hearth stock.
  /// Keyed on the code's leading segments so every metal/variant of a form lands on the same art.
  /// </summary>
  public static Stock? StockOf(string? itemPath)
  {
    if (string.IsNullOrEmpty(itemPath))
      return null;
    if (itemPath.StartsWith("stock-bloom"))
      return Stock.ShingledBloom;
    if (itemPath.StartsWith("stock-slab"))
      return Stock.ShingledSlab;
    if (itemPath.StartsWith("castbillet"))
      return Stock.CastBillet;
    if (itemPath.StartsWith("castbloom"))
      return Stock.CastBloom;
    if (itemPath.StartsWith("castslab"))
      return Stock.CastSlab;
    return null;
  }

  /// <summary>
  /// Every element to draw for the given contents - the structural groups always, plus one stock element
  /// per loaded row. A null entry is an empty row.
  /// </summary>
  public static string[] ElementsFor(IReadOnlyList<Stock?> rows)
  {
    var els = new List<string> { "Base/*", "BaseExtension/*", "Bed/*" };
    foreach (HearthRows.Row row in HearthRows.All)
      if (rows[(int)row] is { } stock)
        els.Add(Element(row, stock));
    return [.. els];
  }
}
