using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Remaps the old single-code sand casting cell to its new brick+facing variant. The cell used to ship
/// codeless (<c>iwex:sandcastingcell</c>) - it had no variant groups at all, so despite its
/// <c>HorizontalOrientable</c> behaviour it never actually oriented. It gained an 8-state <c>brick</c>
/// group (fire first) and an explicit <c>side</c> group, so its code became
/// <c>sandcastingcell-{brick}-{side}</c>; the old cells were the fire-brick look facing north, so they map
/// to <c>iwex:sandcastingcell-fire-north</c> with their saved state copied verbatim (same
/// <c>BlockEntitySandCastingCell</c> class both sides).
/// </summary>
public class SandCastingCellBrickMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Sand casting cell -> brick + facing variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "sandcastingcell-fire-north");
    if (api.World.GetBlock(newCode) == null)
      yield break; // new block not registered (shouldn't happen) - nothing safe to remap to
    yield return (new AssetLocation("iwex", "sandcastingcell"), newCode);
  }

  /// <summary>Copies the cell's saved state verbatim - same class, identical tree.</summary>
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
