using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// The item counterpart of <see cref="SmexToIwexMigration"/>: rewrites the standalone items that moved
/// from the <c>smex</c> domain to <c>iwex</c> with the molten/ironmaking split - the blast mix, slag
/// and powdered slag - so stacks of them in chests, ground storage and player inventories survive the
/// move. Like the block migration it enumerates the registered <c>iwex</c> items and pairs each
/// relocated one with its old <c>smex</c> code, variant-for-variant; the new <c>iwex:burden</c> item
/// never existed under <c>smex</c> and is excluded.
/// </summary>
public class SmexToIwexItemMigration : IItemCodeMigration
{
  public string Name => "Blast mix / slag items moved smex -> iwex";

  // First code part of every item relocated from smex to iwex. burden is new (not a smex rename) and
  // is absent on purpose.
  private static readonly HashSet<string> Relocated =
  [
    "blastmix",
    "slag",
    "powderedslag",
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Item item in api.World.Items)
    {
      if (
        item?.Code == null
        || item.Code.Domain != "iwex"
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
