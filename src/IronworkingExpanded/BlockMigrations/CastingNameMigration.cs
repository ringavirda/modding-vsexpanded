using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// The 2026-08-05 casting rename, for items already in a world: pattern and diagram variants whose
/// <c>type</c> was renamed onto the drawn art's names.
/// <para>
/// <b>Why this is not optional.</b> A pattern is a <b>durable tool the player carved</b>, from a diagram
/// they drafted, out of stock they gathered - and both live in inventories, chests and ground storage. An
/// item whose code stops resolving is silently dropped on world load; there is no error and no way for the
/// player to tell it from a bug. That is the cost of every rename in this file, and it is why the table
/// below is written out per variant rather than left to "it is pre-release".
/// </para>
/// <para>
/// <b>Three types were not renamed but retired</b>, and those are deliberately absent: the
/// <c>moldplate</c> pattern and its diagram (the plate mold is gone - <c>game:metalplate-*</c> is a rolled
/// product, so casting it was a second route to the mill's own output) and the <c>molten-barrel</c> structure
/// diagram (the barrel's cast route is planned by the <c>item-castbarrel</c> pattern diagram, which makes the
/// blank it is lined from). Remapping a retired code onto a surviving one would hand the player an item they
/// never had; letting it lapse is the honest outcome.
/// </para>
/// </summary>
public class CastingNameMigration : IItemCodeMigration
{
  public string Name => "Casting pattern/diagram types renamed onto the drawn art (2026-08-05)";

  /// <summary>
  /// old <c>type</c> → new <c>type</c>. The plurals are not a typo: a pattern is named for what its
  /// impression yields, so the three-lane billet pattern is <c>castbillets</c> while the item it casts stays
  /// a singular <c>caststock-billet</c>.
  /// </summary>
  private static readonly Dictionary<string, string> Types = new()
  {
    ["heavyplate"] = "castheavyplate",
    ["molddoubleingot"] = "castingotmold",
    ["castbillet"] = "castbillets",
    ["castbloom"] = "castblooms",
  };

  /// <summary>Structure-diagram types, which have no pattern behind them - the two furnace-core plans that
  /// were shipped as initialisms.</summary>
  private static readonly Dictionary<string, string> DiagramOnly = new()
  {
    ["bfc"] = "furnace-coldblast",
    ["cf"] = "furnace-cupola",
  };

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    // Walked off the REGISTERED items rather than written out by hand, so the twelve wood variants of each
    // pattern are covered without a twelve-row table that could fall out of step with PatternWoods.
    foreach (Item item in api.World.Items)
    {
      if (item?.Code is not { Domain: "iwex" } code)
        continue;

      string path = code.Path;

      foreach ((string oldType, string newType) in Types)
      {
        // pattern-{type}-{wood}
        if (path.StartsWith($"pattern-{newType}-"))
          yield return (
            new AssetLocation("iwex", $"pattern-{oldType}-{path[$"pattern-{newType}-".Length..]}"),
            code.Clone()
          );

        // diagram-item-{type}
        if (path == $"diagram-item-{newType}")
          yield return (new AssetLocation("iwex", $"diagram-item-{oldType}"), code.Clone());
      }

      foreach ((string oldType, string newType) in DiagramOnly)
        if (path == $"diagram-{newType}")
          yield return (new AssetLocation("iwex", $"diagram-{oldType}"), code.Clone());
    }
  }
}
