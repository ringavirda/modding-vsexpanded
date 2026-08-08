using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Remaps the old single-code sand casting cell to its new brick+facing variant. The cell originally
/// shipped codeless (<c>iwex:sandcastingcell</c>) - it had no variant groups at all, so despite its
/// <c>HorizontalOrientable</c> behaviour it never actually oriented. It gained an 8-state <c>brick</c>
/// group (fire first) and an explicit <c>side</c> group, so its code became
/// <c>sandcastingcell-{brick}-{side}</c>; the old cells were the fire-brick look facing north, so they map
/// to <c>iwex:casting-sandcell-fire-n</c> with their saved state copied verbatim (same
/// <c>BlockEntitySandCastingCell</c> class both sides).
/// <para>
/// The target has moved twice and neither move is a rename of this migration's <b>source</b>: the
/// naming wave took the path to <c>casting/sandcell</c>, and the 2026-08-04 respelling took the facing
/// from <c>north</c> to <c>n</c>. The source string is a historical fact and stays untouched.
/// </para>
/// </summary>
public class SandCastingCellBrickMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Sand casting cell -> brick + facing variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    var newCode = new AssetLocation("iwex", "casting-sandcell-fire-n");
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
