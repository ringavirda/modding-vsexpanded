using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded.BlockMigrations;

/// <summary>
/// The flat <c>ppex → lpex</c> mod-rename migration: every block and item this mod ships was authored
/// under the old <c>ppex</c> domain and is now <c>lpex</c>, so a save from any pre-rename version holds
/// <c>ppex:*</c> codes that no longer resolve. Remaps each currently-registered <c>lpex:{path}</c>
/// block/item from its <c>ppex:{path}</c> predecessor (path preserved). Derived from the live registries,
/// so it always covers exactly what the mod ships. The pipe-tier collapse (the dropped iron/steel material
/// axis) is a separate, non-flat remap in <see cref="PipeMigration"/>; the two never collide because their
/// source codes differ (material-suffixed vs not). Pairs whose old code is absent in a world are skipped.
/// </summary>
public class LpexRenameMigration : IBlockCodeMigration, IItemCodeMigration
{
  public string Name => "ppex → lpex mod rename";

  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> IBlockCodeMigration.GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Block block in api.World.Blocks)
      if (block?.Code is { Domain: "lpex" } code)
        yield return (new AssetLocation("ppex", code.Path), code.Clone());
  }

  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> IItemCodeMigration.GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Item item in api.World.Items)
      if (item?.Code is { Domain: "lpex" } code)
        yield return (new AssetLocation("ppex", code.Path), code.Clone());
  }
}
