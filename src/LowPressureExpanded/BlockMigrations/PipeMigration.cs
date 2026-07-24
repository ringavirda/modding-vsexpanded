using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded.BlockMigrations;

/// <summary>
/// Migrates placed pipes across two historical moves, landing every legacy code on the current
/// tier-split blocks:
///
/// <list type="bullet">
/// <item><description>The pipe base block moved down to iwex and lost its iron/steel <c>material</c>
/// axis (tier = mod now). Old <c>ppex:pipe-{straight,bend,tjunction,xjunction}-{orient}-{iron|steel}</c>
/// segments become the plain bolted <c>iwex:pipe-{type}-{orient}</c>; old
/// <c>ppex:pipe-{valve,pressurevalve}-{orient}-{iron|steel}</c> fittings become the material-less
/// <c>lpex:pipe-{type}-{orient}</c>. (The <c>ppex → lpex</c> mod rename itself - and the brick
/// passthroughs/outlets/fluid-intakes that only need that flat rename - is handled by
/// <see cref="LpexRenameMigration"/>.)</description></item>
/// <item><description>The far older smex→ppex move (<c>gaspipe-* → pipe-*</c>): its segments/valves are
/// retargeted onto the same current tier blocks, its brick shapes onto the lpex fittings (dropped
/// refractory tiers fall back to fire brick), and its removed inline machines onto a plain bolted
/// straight pipe.</description></item>
/// </list>
///
/// The segment/valve remaps are derived from the currently registered blocks (so the orientation lists
/// stay in sync automatically); pairs whose old code is absent in a world are skipped by the migrator.
/// </summary>
public class PipeMigration : IBlockCodeMigration
{
  public string Name => "Pipe tiers: material axis dropped, base moved to iwex";

  private static readonly HashSet<string> SegmentTypes =
    ["straight", "bend", "tjunction", "xjunction"];
  private static readonly HashSet<string> ValveTypes =
    ["valve", "pressurevalve"];
  private static readonly HashSet<string> BrickTypes =
    ["passthrough", "passthroughbend", "outlet"];

  // Old per-facing orientations collapsed onto the surviving straight-pipe axes (legacy inline machines).
  private static readonly Dictionary<string, string> StraightAxis = new()
  {
    ["ns"] = "ns",
    ["sn"] = "ns",
    ["n"] = "ns",
    ["s"] = "ns",
    ["we"] = "we",
    ["ew"] = "we",
    ["w"] = "we",
    ["e"] = "we",
  };

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Block block in api.World.Blocks)
    {
      if (block?.Code == null)
        continue;

      string dom = block.Code.Domain;
      string path = block.Code.Path;
      string? type = block.Variant["type"];
      if (type == null || !path.StartsWith("pipe-"))
        continue;

      // Current bolted segments (iwex): old material-suffixed ppex codes and the older smex gaspipe.
      if (dom == "iwex" && SegmentTypes.Contains(type))
      {
        string orient = block.Variant["orientation"];
        yield return (new AssetLocation("ppex", $"pipe-{type}-{orient}-iron"), block.Code.Clone());
        yield return (new AssetLocation("ppex", $"pipe-{type}-{orient}-steel"), block.Code.Clone());
        yield return (new AssetLocation("smex", $"gaspipe-{type}-{orient}"), block.Code.Clone());
      }
      // Current cast valves (lpex): old material-suffixed ppex codes and the older smex gaspipe.
      else if (dom == "lpex" && ValveTypes.Contains(type))
      {
        string orient = block.Variant["orientation"];
        yield return (new AssetLocation("ppex", $"pipe-{type}-{orient}-iron"), block.Code.Clone());
        yield return (new AssetLocation("ppex", $"pipe-{type}-{orient}-steel"), block.Code.Clone());
        yield return (new AssetLocation("smex", $"gaspipe-{type}-{orient}"), block.Code.Clone());
      }
      // Current brick passthrough/outlet (lpex): only the smex gas-prefix swap remains.
      else if (dom == "lpex" && BrickTypes.Contains(type))
        yield return (new AssetLocation("smex", "gas" + path), block.Code.Clone());
    }

    // Refractory-tier bricks were removed from outlet/passthrough/passthrough-bend; fall back to fire.
    string[] refractory = ["refractorytier1", "refractorytier2", "refractorytier3"];
    (string Type, string[] Orients)[] brickShapes =
    [
      ("outlet", ["s", "n", "w", "e"]),
      ("passthrough", ["ns", "we", "ud"]),
      (
        "passthroughbend",
        ["nw", "se", "en", "ws", "un", "us", "uw", "ue", "dn", "ds", "dw", "de"]
      ),
    ];
    foreach (var (shape, orients) in brickShapes)
    foreach (string tier in refractory)
    foreach (string orient in orients)
      yield return (
        new AssetLocation("smex", $"gaspipe-{shape}-{tier}-{orient}"),
        new AssetLocation("lpex", $"pipe-{shape}-fire-{orient}")
      );

    // Pre-brick legacy passthrough/outlet (before the brick variantgroup existed) → fire brick.
    foreach (string o in new[] { "ns", "we", "ud" })
      yield return (
        new AssetLocation("smex", $"gaspipe-passthrough-{o}"),
        new AssetLocation("lpex", $"pipe-passthrough-fire-{o}")
      );
    foreach (string o in new[] { "s", "n", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-outlet-{o}"),
        new AssetLocation("lpex", $"pipe-outlet-fire-{o}")
      );

    // Removed inline gas machines → a plain bolted (iwex) straight pipe of the matching axis.
    foreach (string o in new[] { "ns", "we" })
      yield return (
        new AssetLocation("smex", $"gaspipe-blower-{o}"),
        new AssetLocation("iwex", $"pipe-straight-{o}")
      );

    string[] heatedBricks =
    [
      "fire", "black", "brown", "cream", "gray", "orange", "red", "tan",
      "refractorytier1", "refractorytier2", "refractorytier3",
    ];
    foreach (string brick in heatedBricks)
    foreach (string o in new[] { "ns", "sn", "we", "ew" })
      yield return (
        new AssetLocation("smex", $"gaspipe-heated-{brick}-{o}"),
        new AssetLocation("iwex", $"pipe-straight-{StraightAxis[o]}")
      );

    foreach (string o in new[] { "n", "s", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-intake-{o}"),
        new AssetLocation("iwex", $"pipe-straight-{StraightAxis[o]}")
      );
  }
}
