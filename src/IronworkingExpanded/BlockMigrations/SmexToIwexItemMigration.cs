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
  //
  // `blastmix` is deliberately absent, not an omission. `iwex:blastmix` itself no longer exists
  // (deleted with the coal-pile charge path), so a remap onto it could not resolve - this walk
  // enumerates registered iwex items and would simply never yield the pair, silently.
  //
  // What happens to a player's stack instead: `smex:blastmix` and `iwex:blastmix` both become
  // unresolvable and the stack is dropped on load. That is the honest outcome and it is deliberately not
  // papered over with a remap to `iwex:burden`, because the two are not the same thing - burden carries a
  // stamped iron/flux/coke mix and blast mix carried none, so a remap would hand the player a burden of
  // an invented grade. The loss is bounded: blast mix was a mixer intermediate, cheap to remake, and
  // the ore mixer that produced it was itself deleted.
  private static readonly HashSet<string> Relocated = ["slag", "powderedslag"];

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
