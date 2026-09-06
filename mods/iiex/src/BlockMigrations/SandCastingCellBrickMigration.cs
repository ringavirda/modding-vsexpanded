using System.Collections.Generic;
using ExpandedLib.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Remaps the variant-less sand casting cell <c>iwex:sandcastingcell</c> onto the current brick + facing
/// variant <c>iiex:casting-sandcell-fire-n</c>: the old cells wore the fire-brick look and never oriented, so
/// they land on fire brick facing north. Same <c>BlockEntitySandCastingCell</c> class both sides, so saved
/// state copies verbatim.
/// <para>
/// The target tracks the current code; the source is historical and must keep its spelling.
/// </para>
/// </summary>
public class SandCastingCellBrickMigration
  : IBlockCodeMigration,
    IBlockEntityMigration {
  public string Name => "Sand casting cell -> brick + facing variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    var newCode = new AssetLocation("iiex", "casting-sandcell-fire-n");
    if (api.World.GetBlock(newCode) == null)
      yield break; // target not registered - nothing safe to remap to
    yield return (new AssetLocation("iwex", "sandcastingcell"), newCode);
  }

  /// <summary>Copies the cell's saved state verbatim - same class, identical tree.</summary>
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
