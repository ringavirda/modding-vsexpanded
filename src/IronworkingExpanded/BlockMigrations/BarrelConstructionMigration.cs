using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Remaps the single-code molten barrel to its new <c>bolted</c> construction variant. The barrel gained a
/// <c>construction</c> variant group (bolted / cast) when the cast route was added, so its code changed
/// from <c>moltenbarrel</c> to <c>moltenbarrel-bolted</c> - bolted being the original fabricated barrel.
/// Placed barrels and held stacks under the old code are rewritten to <c>iwex:moltenbarrel-bolted</c> and
/// their saved contents copied verbatim (both are the same <c>BlockEntityMoltenBarrel</c> class).
/// <para>
/// Covers both the current <c>iwex:moltenbarrel</c> and the pre-split-era <c>smex:moltenbarrel</c>: this
/// supersedes the barrel's leg of <see cref="SmexToIwexMigration"/> (which no longer lists it), so a very
/// old smex world lands on the bolted variant in one hop rather than chaining through the dead
/// <c>iwex:moltenbarrel</c> code the migrator would never resolve.
/// </para>
/// </summary>
public class BarrelConstructionMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Molten barrel -> bolted construction variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "moltenbarrel-bolted");
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
