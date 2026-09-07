using System.Collections.Generic;
using ExpandedLib.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Renames the fabricated molten barrel's construction variant from <c>bolted</c> to <c>plated</c>. Only the
/// code changed - same class, shape and recipe. The construction naming convention (<c>cast</c>,
/// <c>rolled</c>, <c>plated</c>) is in exlib's pipe-network mechanics page.
/// <para>
/// <c>iwex:molten-barrel-bolted</c> is a released code and must keep that spelling: a migration whose source
/// matches nothing does nothing, silently, and orphans every barrel already placed.
/// </para>
/// </summary>
public class BarrelPlatedRenameMigration
  : IBlockCodeMigration,
    IBlockEntityMigration {
  public string Name => "Molten barrel: bolted -> plated";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    var newCode = new AssetLocation("iiex", "molten-barrel-plated");
    if (api.World.GetBlock(newCode) == null)
      yield break; // target not registered - nothing safe to remap to

    // Historical code. Do not rename this literal - see the class remarks.
    yield return (new AssetLocation("iwex", "molten-barrel-bolted"), newCode);
  }

  /// <summary>Copies the barrel's saved state verbatim: the rename touched no stored field, and without the
  /// copy a migrated barrel loads empty.</summary>
  public void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  ) {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
