using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelmakingExpanded.BlockMigrations;

/// <summary>
/// Purges the retired ceramic casting molds from existing worlds, through the shared
/// <see cref="BlockMigrationModSystem"/> sweep. The <c>smex:toolmold-*</c> add-on molds (plate,
/// double-ingot and quad-rod, raw and fired) were replaced by the cast-iron molds cast in the sand cell
/// (see <c>docs/design/machines/casting-cell.md</c>), so a placed or stored one now loads with a code no
/// block owns.
/// <para>
/// The codes are enumerated as literals, not read off the live registry. A removal names blocks that no
/// longer exist by definition, so deriving them from <c>World.Blocks</c> matches nothing and the purge
/// silently declares zero rows - which is what it did. Returning a superset is safe: absent codes are
/// skipped.
/// </para>
/// </summary>
public class CeramicMoldRemoval : IBlockRemoval {
  public string Name => "Retired ceramic casting molds";

  private static readonly string[] ToolTypes =
  [
    "plate",
    "doubleingot",
    "quadrod",
  ];

  /// <summary>Clay colours the molds shipped in, matching vanilla's clay forms. Taken from the
  /// released manifest rather than from memory - <c>earthyorange</c> is easy to miss beside
  /// <c>orange</c>, and a colour left out here is a mold that stays orphaned.</summary>
  private static readonly string[] Colours =
  [
    "black",
    "blue",
    "brown",
    "cream",
    "earthyorange",
    "fire",
    "gray",
    "orange",
    "red",
    "tan",
  ];

  private static readonly string[] States = ["raw", "fired"];

  public IEnumerable<AssetLocation> GetRemovals(ICoreServerAPI api) {
    foreach (string colour in Colours)
      foreach (string state in States)
        foreach (string tool in ToolTypes) {
          yield return new AssetLocation(
            "smex",
            $"toolmold-{colour}-{state}-{tool}"
          );
          // The pre-domain-migration spelling, matched by tool-type suffix so vanilla's own clay
          // tool molds are left untouched.
          yield return new AssetLocation("game", $"toolmold-{colour}-{tool}");
        }
  }
}
