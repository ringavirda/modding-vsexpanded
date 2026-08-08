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
/// ⓘ <b>The cowper-stove intake is deliberately not here, and its own migrator is gone.</b> It gained
/// the same group, then took the side-word form (<c>-north</c>) when it stopped being a network node -
/// so a <c>CowperStoveIntakeOrientationMigration</c> rewrote <c>-n</c> → <c>-north</c>. The 2026-08-04
/// side respelling took the block back to <c>-n</c>, which made that migrator <b>circular</b>: it
/// claimed a <b>live</b> code as its source and pointed at a target no block carries, i.e. it would
/// have silently deleted every placed intake. Since the intake never shipped in any release
/// (<c>ReleasedCodes.Smex</c> lists no <c>cowperstove-intake</c>), the word form only ever existed in
/// dev worlds and is owed nothing. Deleted rather than inverted.
/// </para>
/// <para>
/// The pipe passthrough/outlet that also gained the group have since moved to the lpex mod; their
/// migration lives in <c>PipeMigration</c> there.
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
