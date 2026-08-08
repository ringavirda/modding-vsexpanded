using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// The cast mold family collapsed to one tray on 2026-08-05:
/// <c>casting-mold-doubleingot</c> → <c>casting-mold-ingot</c>.
/// <para>
/// It is a rename <b>and</b> a content change - the drawn tray has one bay, so the block went from
/// <c>requiredUnits 200</c> / two ingots to <c>100</c> / one. A placed mold half-poured under the old rule
/// therefore carries more metal than the new one can hold; its state is copied verbatim regardless, because
/// <see cref="BlockEntities.BlockEntityCastMold"/> reads <c>requiredUnits</c> off the block rather than the
/// tree and treats "already at or past capacity" as a full mold. Losing the overfill would be worse: the
/// player poured that metal.
/// </para>
/// <para>
/// <b><c>casting-mold-plate</c> is deliberately not remapped.</b> It was retired, not renamed:
/// <c>game:metalplate-*</c> is a rolled product, so casting plates in a tray was a second route to what the
/// mill already makes. Remapping it onto the ingot mold would hand the player a different tool than the one
/// they placed, and silently change what their pours yield. A retired block is allowed to lapse.
/// </para>
/// </summary>
public class CastMoldIngotMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Cast mold: doubleingot -> single ingot (2026-08-05)";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "casting-mold-ingot");
    if (api.World.GetBlock(newCode) == null)
      yield break; // new block not registered - nothing safe to remap to
    yield return (new AssetLocation("iwex", "casting-mold-doubleingot"), newCode);
  }

  /// <summary>Same entity class both sides, so the tree copies verbatim.</summary>
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
