using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Processes;

/// <summary>
/// Rewrites held stacks of a stage product that has been renamed, from the <c>formerCodes</c> the stage
/// declares. Discovered by <see cref="BlockMigrationModSystem"/> like any other item migration, so a mod
/// gets the migration by declaring the old code and nothing else.
/// <para>
/// Renames are declared, never detected: exlib sees only the current catalogue, so a code that vanished
/// and one that appeared are indistinguishable from a rename without the hint.
/// </para>
/// </summary>
public class ProcessItemRenames : IItemCodeMigration {
  public string Name => "process stage products";

  /// <summary>The pairs the registry's current contents call for. Runs at server start, by which point
  /// <c>AssetsFinalize</c> has populated the registry.</summary>
  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) => Remaps(StageLadderRegistry.Shared.Families.Count == 0 ? [] : Ladders());

  private static IEnumerable<StageLadder> Ladders() {
    foreach (string family in StageLadderRegistry.Shared.Families)
      if (StageLadderRegistry.Shared.Ladder(family) is { } ladder)
        yield return ladder;
  }

  /// <summary>Every <c>(former, current)</c> pair <paramref name="ladders"/> declares. A stage naming no
  /// code has nothing to be renamed to; an opted-out one still carries its rename, because the stacks in
  /// a player's world do not care which mod built the item.</summary>
  public static IEnumerable<(AssetLocation Old, AssetLocation New)> Remaps(
    IEnumerable<StageLadder> ladders
  ) {
    foreach (StageLadder ladder in ladders)
      foreach (ProcessStage stage in ladder.Stages) {
        if (stage.Code == null)
          continue;
        foreach (string former in stage.FormerCodes)
          yield return (new AssetLocation(former), new AssetLocation(stage.Code));
      }
  }
}
