using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using IronIndustryExpanded.BlockStructures.Forming;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Remaps stock whose <see cref="StockForm"/> was renamed onto the settled route - <c>bloom</c> and
/// <c>slab</c> onto <c>shingledbar</c> and <c>shingledslab</c> - so pieces in inventories, chests and ground
/// storage survive it. An item whose code stops resolving is dropped on world load without an error.
/// <para>
/// Walked off <see cref="StockForm.FormerNames"/> rather than written out here, so a form that declares an
/// old name gets its migration free and this file never has to be edited again - the same contract the stage
/// route's <c>formerCodes</c> offers. The form name a rolled piece carries on its own stack is resolved by
/// the registry itself and rewritten the next time the piece is fed.
/// </para>
/// </summary>
public class StockFormRenameMigration : IItemCodeMigration {
  public string Name =>
    "Rolling stock renamed onto the settled forms (2026-08-12)";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach (StockForm form in StockForm.All.Values)
      foreach (string former in form.FormerNames)
        yield return (
          new AssetLocation("iwex", $"stock-{former}"),
          new AssetLocation("iiex", $"stock-{form.Name}")
        );
  }
}
