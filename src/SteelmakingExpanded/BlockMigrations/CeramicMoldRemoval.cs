using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelmakingExpanded.BlockMigrations;

/// <summary>
/// Purges the retired ceramic casting molds from existing worlds. The <c>smex:toolmold-*</c> add-on molds
/// (plate / double-ingot / quad-rod, raw and fired) were removed in favour of the cast-iron molds cast in
/// the sand cell (iwex; see <c>docs/design/sand-casting.md</c>), so a placed or stored one now loads with
/// a code no block owns. This deletes them from the world and from inventories on the next load, through
/// the shared <see cref="BlockMigrationModSystem"/> sweep.
/// <para>
/// Also catches the pre-domain-migration <c>game:toolmold-*-{plate,doubleingot,quadrod}</c> variants (very
/// old worlds that never ran the game→smex move) - matched by their tool-type suffix so vanilla's own
/// tool molds (axe, pickaxe, …) are left untouched. Returning a superset is safe; absent codes are skipped.
/// </para>
/// </summary>
public class CeramicMoldRemoval : IBlockRemoval
{
  public string Name => "Retired ceramic casting molds";

  private static readonly string[] ToolTypes = ["plate", "doubleingot", "quadrod"];

  public IEnumerable<AssetLocation> GetRemovals(ICoreServerAPI api) =>
    api
      .World.Blocks.Where(b => b?.Code != null && IsRetiredMold(b.Code))
      .Select(b => b.Code);

  private static bool IsRetiredMold(AssetLocation code)
  {
    string path = code.Path;
    if (!path.StartsWith("toolmold-"))
      return false;
    // Every smex tool mold was one of these add-on molds; vanilla clay molds we only touch when the code
    // ends in one of the added tool types.
    return code.Domain == "smex"
      || (code.Domain == "game" && ToolTypes.Any(t => path.EndsWith("-" + t)));
  }
}
