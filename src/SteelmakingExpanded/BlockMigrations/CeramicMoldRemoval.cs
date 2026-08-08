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
/// block owns. The sweep also covers the pre-domain-migration
/// <c>game:toolmold-*-{plate,doubleingot,quadrod}</c> variants, matched by tool-type suffix so vanilla's
/// own tool molds are left untouched. Returning a superset is safe: absent codes are skipped.
/// </summary>
public class CeramicMoldRemoval : IBlockRemoval {
  public string Name => "Retired ceramic casting molds";

  private static readonly string[] ToolTypes =
  [
    "plate",
    "doubleingot",
    "quadrod",
  ];

  public IEnumerable<AssetLocation> GetRemovals(ICoreServerAPI api) =>
    api
      .World.Blocks.Where(b => b?.Code != null && IsRetiredMold(b.Code))
      .Select(b => b.Code);

  private static bool IsRetiredMold(AssetLocation code) {
    string path = code.Path;
    if (!path.StartsWith("toolmold-"))
      return false;
    // Every smex tool mold is one of these add-on molds; a vanilla clay mold matches only when its code
    // ends in one of the added tool types.
    return code.Domain == "smex"
      || (code.Domain == "game" && ToolTypes.Any(t => path.EndsWith("-" + t)));
  }
}
