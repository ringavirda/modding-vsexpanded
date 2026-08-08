using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Remaps the casting patterns and diagrams whose <c>type</c> was renamed onto the drawn art's names, so
/// carved patterns and drafted diagrams in inventories, chests and ground storage survive the rename. An item
/// whose code stops resolving is dropped on world load without an error.
/// <para>
/// Three types were retired rather than renamed and are absent on purpose: the <c>moldplate</c> pattern, its
/// diagram, and the <c>molten-barrel</c> structure diagram. Remapping a retired code onto a surviving one
/// would hand the player an item they never had.
/// </para>
/// </summary>
public class CastingNameMigration : IItemCodeMigration {
  public string Name =>
    "Casting pattern/diagram types renamed onto the drawn art (2026-08-05)";

  /// <summary>
  /// old <c>type</c> → new <c>type</c>. The plurals are intended: a pattern is named for what its impression
  /// yields, so the three-lane billet pattern is <c>castbillets</c> while the item it casts stays a singular
  /// <c>caststock-billet</c>.
  /// </summary>
  private static readonly Dictionary<string, string> Types = new() {
    ["heavyplate"] = "castheavyplate",
    ["molddoubleingot"] = "castingotmold",
    ["castbillet"] = "castbillets",
    ["castbloom"] = "castblooms",
  };

  /// <summary>Structure-diagram types, which have no pattern behind them: the two furnace-core plans, whose
  /// old codes were initialisms.</summary>
  private static readonly Dictionary<string, string> DiagramOnly = new() {
    ["bfc"] = "furnace-coldblast",
    ["cf"] = "furnace-cupola",
  };

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    // Walked off the registered items rather than written out by hand, so the twelve wood variants of each
    // pattern are covered without a table that could fall out of step with PatternWoods.
    foreach (Item item in api.World.Items) {
      if (item?.Code is not { Domain: "iwex" } code)
        continue;

      string path = code.Path;

      foreach ((string oldType, string newType) in Types) {
        // pattern-{type}-{wood}
        if (path.StartsWith($"pattern-{newType}-"))
          yield return (
            new AssetLocation(
              "iwex",
              $"pattern-{oldType}-{path[$"pattern-{newType}-".Length..]}"
            ),
            code.Clone()
          );

        // diagram-item-{type}
        if (path == $"diagram-item-{newType}")
          yield return (
            new AssetLocation("iwex", $"diagram-item-{oldType}"),
            code.Clone()
          );
      }

      foreach ((string oldType, string newType) in DiagramOnly)
        if (path == $"diagram-{newType}")
          yield return (
            new AssetLocation("iwex", $"diagram-{oldType}"),
            code.Clone()
          );
    }
  }
}
