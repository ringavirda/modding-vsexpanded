using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Merges the two frozen-melt blocks into one variant-grouped block: <c>iwex:solidifiediron</c> becomes
/// <c>iiex:hearthmetal-pigiron</c>, <c>iwex:solidifiedcastiron</c> becomes <c>iiex:hearthmetal-castiron</c>.
/// Same class, same entity, same stored count - the metal moves from a block attribute into the code.
/// Neither source is a released code, so this covers dev and playtest worlds only; the released
/// <c>smex:solidifiediron</c> reaches <c>hearthmetal-pigiron</c> through <see cref="SmexToIiexMigration"/>.
/// Rows are emitted per variant, and <c>BlockMigrationModSystem</c> drops a pair whose target is
/// unregistered with a <c>Logger.Warning</c>, so a partial registry costs only the rows it cannot serve.
/// </summary>
public class HearthMetalMigration : IBlockCodeMigration, IBlockEntityMigration {
  public string Name =>
    "Hearth metal: solidifiediron/solidifiedcastiron -> hearthmetal-{metal}";

  // (historical iwex base code, live iwex base code). The old spelling is what a saved world contains and
  // must not be corrected; a migration whose source matches nothing does nothing, silently.
  private static readonly (string Old, string New)[] Merged =
  [
    ("solidifiediron", "hearthmetal-pigiron"),
    ("solidifiedcastiron", "hearthmetal-castiron"),
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach ((string old, string live) in Merged)
      foreach (var pair in CodeRelocation.Remap(api, "iwex", old, "iiex", live))
        yield return pair;
  }

  /// <summary>
  /// Copies the saved tree verbatim. The merge touched no stored field, so the entity reads what it always
  /// read; without the copy the stored count resets to its default.
  /// </summary>
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
