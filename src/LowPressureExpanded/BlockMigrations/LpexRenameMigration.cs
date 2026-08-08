using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded.BlockMigrations;

/// <summary>
/// The <c>ppex → lpex</c> mod-rename migration. Everything this mod ships was authored under the old
/// <c>ppex</c> domain, so a save from any pre-rename version holds <c>ppex:*</c> codes that no longer
/// resolve.
///
/// <para>
/// ⚠ <b>Each row names the base code as it shipped in ppex 0.6.4</b> rather than deriving it from the
/// live registry. The derived form (<c>ppex:{code.Path}</c> for every live lpex block) is correct only
/// while no path ever changes: after a rename it emits old codes that never shipped and silently stops
/// covering the ones that did, with nothing but a <c>Logger.Warning</c> to show for it. Variants are
/// still carried from the live registry, which is the half that should track the present.
/// </para>
///
/// <para>
/// ⓘ <b>Not every ppex block is here, and that is the point.</b> The Lancashire boiler and Cornish
/// engine left for hpex (<c>HpexExtractionMigration</c>); the pipe <i>segments</i> and valves left for
/// iwex/lpex without their <c>material</c> axis (<c>PipeMigration</c>). What remains is what is still
/// an lpex block under an unchanged base code: the Cornish boiler, the Watt engine and its two docked
/// sub-machines, the manual pump, the steam condenser, and the four pipe fittings that never carried a
/// material axis to drop.
/// </para>
/// </summary>
public class LpexRenameMigration : IBlockCodeMigration, IItemCodeMigration
{
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
    // ⚠ The four fittings that shipped with NO material variant, so a flat rename is all they need.
    // Listed individually rather than as a bare "pipe" base: the segments and valves DID carry the
    // material axis and belong to PipeMigration, and a "pipe" base here would claim them first.
    // The trailing-separator rule in CodeRelocation is what keeps `pipe-passthrough` from also
    // swallowing `pipe-passthroughbend`.
    "pipe-outlet",
    "pipe-passthrough",
    "pipe-passthroughbend",
    "pipe-fluidintake",
  ];

  /// <summary>The two items ppex shipped; both are still lpex items under the same name.</summary>
  private static readonly string[] Items = ["gear", "largegear"];

  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> IBlockCodeMigration.GetRemaps(
    ICoreServerAPI api
  )
  {
    // ⚠ legacySideWords: ppex spelled its `side` variant as a full word (`ppex:boilercornish-north`),
    // and every side group in this suite renders a letter now. Without it the six machines' 24 released
    // codes are orphaned. ⓘ Harmless on the four fittings below it: they carry an `orientation` group,
    // not a `side` one, so the respelling does not fire and their `-n` codes stay as they shipped.
    foreach (string @base in Blocks)
      foreach (var pair in CodeRelocation.Remap(api, "ppex", "lpex", @base, legacySideWords: true))
        yield return pair;
  }

  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> IItemCodeMigration.GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Item item in api.World.Items)
    {
      if (item?.Code is not { Domain: "lpex" } code)
        continue;

      foreach (string @base in Items)
        if (
          code.Path == @base
          || code.Path.StartsWith(@base + "-", System.StringComparison.Ordinal)
        )
        {
          yield return (new AssetLocation("ppex", code.Path), code.Clone());
          break;
        }
    }
  }
}
