using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Storage;

/// <summary>
/// How many bay cells a stack of one item occupies. Declared, never measured off a drawn shape's bounding
/// box: capacity has to be answerable before anything is tesselated, and an item whose art changes must
/// not silently change what a rack holds.
/// </summary>
/// <param name="Item">Item or block code the rule is for. A trailing <c>*</c> matches a family, so a
/// whole stock ladder is one line.</param>
/// <param name="Cells">Cells a stack of it fills. At least one.</param>
public sealed record BayOccupancy(string Item, int Cells) {
  /// <summary>What an item occupies when nothing declares it - one cell, the smallest thing a row can
  /// hold.</summary>
  public const int DefaultCells = 1;

  /// <summary>Whether this rule is the one for <paramref name="code"/>.</summary>
  public bool Matches(string code) =>
    Item.EndsWith('*')
      ? code.StartsWith(Item[..^1], StringComparison.OrdinalIgnoreCase)
      : string.Equals(Item, code, StringComparison.OrdinalIgnoreCase);

  /// <summary>How specific this rule is, so an exact code beats a family wildcard and a longer prefix
  /// beats a shorter one. An exact match is longer than any prefix that could reach it.</summary>
  public int Precision => Item.EndsWith('*') ? Item.Length - 1 : int.MaxValue;
}

/// <summary>
/// One file's worth of occupancy rules, plus the store they are for. Keyed by store rather than global,
/// so a rack and a future crate can size the same item differently.
/// </summary>
/// <param name="Store">Store key the rules belong to, matching the machine's own
/// <c>BayStoreKey</c>.</param>
/// <param name="Rules">The declarations, in file order.</param>
public sealed record BayOccupancySet(
  string Store,
  IReadOnlyList<BayOccupancy> Rules
) {
  /// <summary>
  /// Reads one catalogue file. A file naming no store, or carrying a rule with no item or a cell count
  /// under one, is refused whole with a message naming what is wrong - a half-read catalogue would size
  /// some items and silently default the rest.
  /// </summary>
  public static bool TryParse(
    JsonObject json,
    out BayOccupancySet? set,
    out string? error
  ) {
    set = null;
    error = null;

    string? store = json["store"].AsString(null);
    if (string.IsNullOrWhiteSpace(store)) {
      error =
        "no 'store' key, so nothing knows which store these rules are for";
      return false;
    }

    var rules = new List<BayOccupancy>();
    JsonObject[] declared = json["items"]?.AsArray() ?? [];
    foreach (JsonObject entry in declared) {
      string? item = entry["item"].AsString(null);
      if (string.IsNullOrWhiteSpace(item)) {
        error = $"{store}: a rule has no 'item' code";
        return false;
      }

      int cells = entry["cells"].AsInt(BayOccupancy.DefaultCells);
      if (cells < 1) {
        error =
          $"{store}: '{item}' occupies {cells} cells; a stored stack takes at least one";
        return false;
      }
      rules.Add(new BayOccupancy(item, cells));
    }

    set = new BayOccupancySet(store, rules);
    return true;
  }
}
