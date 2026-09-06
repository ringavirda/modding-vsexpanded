using System.Collections.Generic;
using ExpandedLib.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// The item counterpart of <see cref="SmexToIiexMigration"/>: rewrites the standalone items that moved from
/// the <c>smex</c> domain to <c>iwex</c> with the molten/ironmaking split, so stacks in chests, ground storage
/// and player inventories survive the move. Enumerates the registered <c>iwex</c> items and pairs each
/// relocated one with its old <c>smex</c> code, variant-for-variant.
/// </summary>
public class SmexToIiexItemMigration : IItemCodeMigration {
  public string Name => "Blast mix / slag items moved smex -> iwex";

  // First code part of every item relocated from smex to iwex. Two codes are absent on purpose:
  // `burden` is new rather than a smex rename, and `blastmix` no longer exists on either side, so its
  // stacks become unresolvable and are dropped on load. Blast mix is not remapped onto `burden`: burden
  // carries a stamped iron/flux/coke mix and blast mix carried none.
  private static readonly HashSet<string> Relocated = ["slag", "powderedslag"];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach (Item item in api.World.Items) {
      if (
        item?.Code == null
        || item.Code.Domain != "iiex"
        || !Relocated.Contains(item.Code.FirstCodePart())
      )
        continue;
      yield return (
        new AssetLocation("smex", item.Code.Path),
        item.Code.Clone()
      );
    }
  }
}
