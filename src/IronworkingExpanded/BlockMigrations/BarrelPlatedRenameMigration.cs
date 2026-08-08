using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Renames the fabricated molten barrel's construction variant from <c>bolted</c> to <c>plated</c>
/// (2026-08-05). Nothing about the block changed - same class, same shape, same recipe - only the word.
/// <para>
/// <b>Why the word changed.</b> "Bolted" named the barrel's <i>joint</i>, not how the vessel was made, and
/// the other two members of that family are named for their fabrication (<c>cast</c>, <c>rolled</c>). A
/// vessel hammered up from flat stock is a <i>plate</i> vessel; the bolts, where there are any, hold
/// flanges. The pipe tier moved in the same edit for the same reason - see
/// <c>docs/design/mechanics/pipe-network.md</c>.
/// </para>
/// <para>
/// Caution: <b><c>iwex:molten-barrel-bolted</c> is a source and must stay spelled that way forever.</b> It was a
/// released code, so a saved world contains it. This is the exact trap
/// <see cref="BarrelConstructionMigration"/> documents: a search-and-replace that "tidies" this literal
/// makes the migration internally consistent, matching nothing, and orphans every barrel ever placed -
/// with no test failing, because a migration that matches nothing simply does nothing.
/// </para>
/// </summary>
public class BarrelPlatedRenameMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Molten barrel: bolted -> plated";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "molten-barrel-plated");
    if (api.World.GetBlock(newCode) == null)
      yield break; // new block not registered (shouldn't happen) - nothing safe to remap to

    // Historical code. Do not rename this literal - see the class remarks.
    yield return (new AssetLocation("iwex", "molten-barrel-bolted"), newCode);
  }

  /// <summary>Copies the barrel's saved state verbatim - same class, identical tree, a name change only.
  /// The rename touched no field, so nothing needs translating; the tree just has to be carried across, or
  /// every migrated barrel comes back empty.</summary>
  public void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  )
  {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
