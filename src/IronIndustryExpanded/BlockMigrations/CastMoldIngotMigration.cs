using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Collapses the cast mold family to one tray: <c>casting-mold-doubleingot</c> becomes
/// <c>casting-mold-ingot</c>. This is a rename and a capacity change - the single-bay tray holds
/// <c>requiredUnits 100</c> / one ingot where the old block held <c>200</c> / two.
/// <para>
/// A mold poured past the new capacity keeps its contents: the state copies verbatim because
/// <see cref="BlockStructures.Casting.BlockEntities.BlockEntityCastMold"/> reads <c>requiredUnits</c> off the block rather than the
/// tree and treats at-or-past capacity as full. <c>casting-mold-plate</c> was retired rather than renamed,
/// so it is intentionally absent from the table.
/// </para>
/// </summary>
public class CastMoldIngotMigration : IBlockCodeMigration, IBlockEntityMigration {
  public string Name => "Cast mold: doubleingot -> single ingot (2026-08-05)";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    var newCode = new AssetLocation("iiex", "casting-mold-ingot");
    if (api.World.GetBlock(newCode) == null)
      yield break; // new block not registered - nothing safe to remap to
    yield return (
      new AssetLocation("iwex", "casting-mold-doubleingot"),
      newCode
    );
  }

  /// <summary>Same entity class both sides, so the tree copies verbatim.</summary>
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
