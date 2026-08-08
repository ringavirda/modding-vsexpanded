using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Remaps the single-code molten barrel to its <c>construction</c> variant. The barrel gained that variant
/// group when the cast route was added, so its code changed from <c>moltenbarrel</c> to the fabricated
/// variant - which was spelled <c>molten-barrel-bolted</c> at the time and is spelled
/// <c>molten-barrel-plated</c> today (renamed 2026-08-05; see <see cref="BarrelPlatedRenameMigration"/>).
/// Placed barrels and held stacks under the old code are rewritten straight to the CURRENT code and their
/// saved contents copied verbatim (both are the same <c>BlockEntityMoltenBarrel</c> class).
/// <para>
/// Covers both the old <c>iwex:moltenbarrel</c> and the pre-split-era <c>smex:moltenbarrel</c>: this
/// supersedes the barrel's leg of <see cref="SmexToIwexMigration"/> (which no longer lists it), so a very
/// old smex world lands on the fabricated variant in one hop rather than chaining through the dead
/// <c>iwex:moltenbarrel</c> code the migrator would never resolve.
/// </para>
/// <para>
/// Caution: <b>both source codes are pre-rename and must stay spelled that way.</b> The barrel later
/// moved to <c>molten-barrel</c>, and a search-and-replace over this file once rewrote the sources
/// too - which would have made the migration internally consistent, matching nothing, and orphaning every
/// released barrel with no test failing. A migration source is a historical fact, not a current code.
/// </para>
/// <para>
/// <b>The target is the opposite: it is always the current code.</b> The <c>bolted</c> →
/// <c>plated</c> rename moved it, deliberately, so an ancient world still lands in one hop instead of
/// chaining through a code that no longer registers - <see cref="GetRemaps"/> bails out entirely when its
/// target is unregistered, so a stale target would silently orphan every pre-variant barrel. That same
/// rename also rewrote this comment's history on the way past, which is the warning above one level up:
/// prose that records what a code used to be is a historical fact too.
/// </para>
/// </summary>
public class BarrelConstructionMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Molten barrel -> plated construction variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "molten-barrel-plated");
    if (api.World.GetBlock(newCode) == null)
      yield break; // new block not registered (shouldn't happen) - nothing safe to remap to
    yield return (new AssetLocation("iwex", "moltenbarrel"), newCode);
    yield return (new AssetLocation("smex", "moltenbarrel"), newCode);
  }

  /// <summary>Copies the barrel's saved state verbatim - same class, identical tree.</summary>
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
