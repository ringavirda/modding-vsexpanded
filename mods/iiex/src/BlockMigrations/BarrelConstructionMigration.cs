using System.Collections.Generic;
using ExpandedLib.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Remaps the single-code molten barrel - both <c>iwex:moltenbarrel</c> and the pre-split
/// <c>smex:moltenbarrel</c> - onto the current fabricated variant <c>iiex:molten-barrel-plated</c>, in one hop.
/// Both sides are the same <c>BlockEntityMoltenBarrel</c> class, so saved contents copy verbatim. Supersedes
/// the barrel's leg of <see cref="SmexToIiexMigration"/>, which no longer lists it.
/// <para>
/// The two source codes are historical and must keep their pre-rename spelling; a migration whose source
/// matches nothing does nothing, silently. The target is the opposite and always tracks the current code,
/// because <see cref="GetRemaps"/> yields nothing at all when its target is unregistered.
/// </para>
/// </summary>
public class BarrelConstructionMigration
  : IBlockCodeMigration,
    IBlockEntityMigration {
  public string Name => "Molten barrel -> plated construction variant";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    var newCode = new AssetLocation("iiex", "molten-barrel-plated");
    if (api.World.GetBlock(newCode) == null)
      yield break; // target not registered - nothing safe to remap to
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
  ) {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
