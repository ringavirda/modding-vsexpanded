using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelIndustryExpanded.BlockMigrations;

/// <summary>
/// Migrates the smoke-stack intake, which gained a brick/refractory-tier variantgroup: placements of
/// the pre-group code (<c>smex:smokestack-intake-s</c>) load as missing-block placeholders and are
/// rewritten to the tier3 variant of the same base and orientation. The pipe passthrough and outlet
/// that gained the same group migrate in iiex's <c>PipeMigration</c>. The cowper-stove intake has no
/// migrator: it never shipped (<c>ReleasedCodes.Smex</c> lists no <c>cowperstove-intake</c>) and its
/// pre-group code is live again, so a remap would claim a live code as its source.
/// </summary>
public class BrickVariantMigration : IBlockCodeMigration {
  /// <summary>One block that gained a variant: its pre-group code, the variant value inserted before
  /// the orientation, and the orientations that existed beforehand.</summary>
  private readonly record struct Entry(
    string CodeBase,
    string InsertedVariant,
    string[] Orientations
  );

  private static readonly Entry[] Entries =
  [
    new("smokestack-intake", "tier3", ["n", "s", "w", "e"]),
  ];

  public string Name => "Brick and refractory-tier variants";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach (var (codeBase, inserted, orientations) in Entries)
      foreach (string orient in orientations)
        yield return (
          new AssetLocation("smex", $"{codeBase}-{orient}"),
          new AssetLocation("siex", $"{codeBase}-{inserted}-{orient}")
        );
  }
}
