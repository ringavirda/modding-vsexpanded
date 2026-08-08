using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded.BlockMigrations;

/// <summary>
/// The <c>ppex → lpex</c> mod-rename migration: a pre-rename save holds <c>ppex:*</c> codes that no
/// longer resolve, so every surviving block and item is remapped onto its lpex code. Rows name the base
/// code as it shipped in ppex 0.6.4 rather than deriving it from the live registry, which would emit
/// codes that never shipped; variants are still carried from the live registry. Covers only what is
/// still an lpex block under an unchanged base code. The Lancashire boiler and Cornish engine moved to
/// hpex (<c>HpexExtractionMigration</c>); the pipe segments and valves lost their <c>material</c> axis
/// (<c>PipeMigration</c>).
/// </summary>
public class LpexRenameMigration : IBlockCodeMigration, IItemCodeMigration {
  public string Name => "ppex → lpex mod rename";

  /// <summary>Base codes that shipped under ppex and are still lpex blocks under the same name.</summary>
  private static readonly string[] Blocks =
  [
    "boilercornish",
    "enginewatt",
    "enginefluidpump",
    "enginempgenerator",
    "manualfluidpump",
    "steamcondenser",
    // The four fittings that shipped with no material variant, so a flat rename is all they need. Listed
    // individually because a bare "pipe" base would also claim the segments and valves PipeMigration
    // owns; CodeRelocation's trailing-separator rule keeps `pipe-passthrough` from swallowing
    // `pipe-passthroughbend`.
    "pipe-outlet",
    "pipe-passthrough",
    "pipe-passthroughbend",
    "pipe-fluidintake",
  ];

  /// <summary>The two items ppex shipped; both are still lpex items under the same name.</summary>
  private static readonly string[] Items = ["gear", "largegear"];

  IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> IBlockCodeMigration.GetRemaps(ICoreServerAPI api) {
    // legacySideWords: ppex spelled its `side` variant as a full word (`ppex:boilercornish-north`) where
    // every side group in this suite now renders a letter. It does not fire on the four fittings, which
    // carry an `orientation` group rather than a `side` one, so their `-n` codes stay as they shipped.
    foreach (string @base in Blocks)
      foreach (
        var pair in CodeRelocation.Remap(
          api,
          "ppex",
          "lpex",
          @base,
          legacySideWords: true
        )
      )
        yield return pair;
  }

  IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> IItemCodeMigration.GetRemaps(ICoreServerAPI api) {
    foreach (Item item in api.World.Items) {
      if (item?.Code is not { Domain: "lpex" } code)
        continue;

      foreach (string @base in Items)
        if (
          code.Path == @base
          || code.Path.StartsWith(@base + "-", System.StringComparison.Ordinal)
        ) {
          yield return (new AssetLocation("ppex", code.Path), code.Clone());
          break;
        }
    }
  }
}
