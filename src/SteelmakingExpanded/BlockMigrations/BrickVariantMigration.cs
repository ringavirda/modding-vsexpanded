using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelmakingExpanded.BlockMigrations;

/// <summary>
/// Migrates the smoke-stack intake, which gained a brick/refractory-tier variantgroup. It originally
/// used a code without that group (e.g. <c>smex:smokestack-intake-s</c>); adding the group changed the
/// code, so old placements load as missing-block placeholders. Each is rewritten to the tier3 variant
/// of the same base and orientation.
/// <para>
/// The cowper-stove intake also gained the group, but its orientation was later switched to the
/// side-word form (<c>-north</c>/<c>-south</c>/…) by <see cref="PpexMigration"/>, so a tier3-<em>letter</em>
/// target here no longer resolves - that remap was dead (skipped every startup) and has been removed.
/// If a pre-tier <c>cowperstove-intake-&lt;letter&gt;</c> placement is ever found in an old world, the fix
/// is a direct letter→side-word remap, not this dead intermediate. The pipe passthrough/outlet that also
/// gained the group have since moved to the ppex mod; their migration lives in <c>PipeMigration</c> there.
/// </para>
/// </summary>
public class BrickVariantMigration : IBlockCodeMigration
{
  /// <summary>
  /// One block that gained a variant: its code without the new group, the variant value
  /// inserted before the orientation, and the orientations that existed beforehand.
  /// </summary>
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
  )
  {
    foreach (var (codeBase, inserted, orientations) in Entries)
    foreach (string orient in orientations)
      yield return (
        new AssetLocation("smex", $"{codeBase}-{orient}"),
        new AssetLocation("smex", $"{codeBase}-{inserted}-{orient}")
      );
  }
}
