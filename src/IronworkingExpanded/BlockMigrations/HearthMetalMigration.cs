using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Merges the two frozen-melt blocks into one variant-grouped block (2026-08-07):
/// <c>iwex:solidifiediron</c> becomes <c>iwex:hearthmetal-pigiron</c> and
/// <c>iwex:solidifiedcastiron</c> becomes <c>iwex:hearthmetal-castiron</c>. Same class, same entity, same
/// stored count - the metal moves from a block attribute into the code.
/// <para>
/// <b>Neither source ever shipped, and that is worth stating rather than assuming.</b>
/// <c>ExpandedLib.Testing.ReleasedCodes</c> has no iwex rows at all, so this migration carries no released
/// debt; it exists so dev and playtest worlds do not lose blocks over a rename. The released code in this
/// family is <c>smex:solidifiediron</c>, and it reaches <c>hearthmetal-pigiron</c> through
/// <see cref="SmexToIwexMigration"/> instead - that row is a contract and
/// <c>ReleasedCodeCoverageTests</c> is the only suite that can see the whole chain.
/// </para>
/// <para>
/// <b>Each row is guarded by its own variant.</b> Emitting a pair whose target is not registered is not
/// an error that surfaces: <c>BlockMigrationModSystem</c> drops an unresolvable pair with a
/// <c>Logger.Warning</c>, so the saved block simply stops loading. Yielding per-variant means a partial
/// registry costs the rows it can serve and no more.
/// </para>
/// </summary>
public class HearthMetalMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Hearth metal: solidifiediron/solidifiedcastiron -> hearthmetal-{metal}";

  // (historical iwex base code, live iwex base code). The left side is frozen - it is what a saved
  // world contains - and a search-and-replace that "tidies" it makes the migration match nothing, which
  // is silent because a migration that matches nothing simply does nothing.
  private static readonly (string Old, string New)[] Merged =
  [
    ("solidifiediron", "hearthmetal-pigiron"),
    ("solidifiedcastiron", "hearthmetal-castiron"),
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach ((string old, string live) in Merged)
      foreach (var pair in CodeRelocation.Remap(api, "iwex", old, "iwex", live))
        yield return pair;
  }

  /// <summary>
  /// Copies the saved tree verbatim. The merge renamed the block and moved the metal into the code; it
  /// touched no stored field, so the entity's own <c>FromTreeAttributes</c> reads what it always read.
  /// Without this the count resets to its default and every migrated block drops two bits regardless of
  /// what was in the hearth.
  /// </summary>
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
