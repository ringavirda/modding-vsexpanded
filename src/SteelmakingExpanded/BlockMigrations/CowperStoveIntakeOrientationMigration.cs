using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelmakingExpanded.BlockMigrations;

/// <summary>
/// Rewrites the cowper-stove intake's orientation variant from the single-letter form
/// (<c>-n</c>/<c>-s</c>/<c>-w</c>/<c>-e</c>) to the side-word form (<c>-north</c>/…) it took when the
/// intake stopped being a network node and started deriving its facing the way every other oriented
/// block does. Old placements otherwise load as missing-block placeholders.
/// <para>
/// Named for its subject rather than its cause: it used to be called <c>LpexMigration</c> after the mod
/// whose change prompted it, which said nothing about what it does and aged badly the moment that mod
/// was renamed. <see cref="BrickVariantMigration"/> covers the same block's earlier tier variantgroup
/// and documents why the two cannot be composed.
/// </para>
/// </summary>
public class CowperStoveIntakeOrientationMigration : IBlockCodeMigration
{
  private readonly record struct Entry(
    string CodeBase,
    string[] Variants,
    string[] OldOrientations,
    string[] NewOrientations
  );

  private static readonly Entry[] Entries =
  [
    new(
      "cowperstove-intake",
      ["tier1", "tier2", "tier3"],
      ["n", "s", "w", "e"],
      ["north", "south", "west", "east"]
    ),
  ];

  public string Name => "Cowper stove intake not network node.";

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (
      var (codeBase, variations, oldOrientations, newOrientations) in Entries
    )
    foreach (string variant in variations)
    foreach (var (i, oldOrient) in oldOrientations.Index())
      yield return (
        new AssetLocation("smex", $"{codeBase}-{variant}-{oldOrient}"),
        new AssetLocation("smex", $"{codeBase}-{variant}-{newOrientations[i]}")
      );
  }
}
