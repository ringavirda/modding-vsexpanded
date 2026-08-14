using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded.BlockMigrations;

/// <summary>
/// Migrates placed pipes across two moves onto the current tier-split blocks. The pipe base moved to
/// iwex and lost its iron/steel <c>material</c> axis, so <c>ppex:pipe-{segment}-{orient}-{iron|steel}</c>
/// becomes <c>iwex:pipe-plated-{type}-{orient}</c> and the valves <c>lpex:pipe-cast-{type}-{orient}</c>. The older
/// smex→ppex move (<c>gaspipe-* → pipe-*</c>) retargets segments and valves onto those same blocks,
/// brick shapes onto the lpex fittings (dropped refractory tiers fall back to fire brick), and removed
/// inline machines onto a plain plated straight pipe. Remaps derive from the registered blocks, so
/// orientation lists stay in sync; pairs whose old code is absent in a world are skipped. The flat
/// <c>ppex → lpex</c> rename is <see cref="LpexRenameMigration"/>.
/// </summary>
public class PipeMigration : IBlockCodeMigration {
  public string Name => "Pipe tiers: material axis dropped, base moved to iwex";

  private static readonly HashSet<string> SegmentTypes =
  [
    "straight",
    "bend",
    "tjunction",
    "xjunction",
  ];
  private static readonly HashSet<string> ValveTypes =
  [
    "valve",
    "pressurevalve",
  ];
  private static readonly HashSet<string> BrickTypes =
  [
    "passthrough",
    "passthroughbend",
    "outlet",
  ];

  // Old per-facing orientations collapsed onto the surviving straight-pipe axes (legacy inline machines).
  private static readonly Dictionary<string, string> StraightAxis = new() {
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
  ) {
    foreach (Block block in api.World.Blocks) {
      if (block?.Code == null)
        continue;

      string dom = block.Code.Domain;
      string path = block.Code.Path;
      string? type = block.Variant["type"];
      if (type == null || !path.StartsWith("pipe-"))
        continue;

      // Current plated segments (iwex): old material-suffixed ppex codes and the older smex gaspipe.
      if (dom == "iwex" && SegmentTypes.Contains(type)) {
        string orient = block.Variant["orientation"];
        yield return (
          new AssetLocation("ppex", $"pipe-{type}-{orient}-iron"),
          block.Code.Clone()
        );
        yield return (
          new AssetLocation("ppex", $"pipe-{type}-{orient}-steel"),
          block.Code.Clone()
        );
        yield return (
          new AssetLocation("smex", $"gaspipe-{type}-{orient}"),
          block.Code.Clone()
        );
      }
      // Current cast valves (lpex): old material-suffixed ppex codes and the older smex gaspipe.
      else if (dom == "lpex" && ValveTypes.Contains(type)) {
        string orient = block.Variant["orientation"];
        yield return (
          new AssetLocation("ppex", $"pipe-{type}-{orient}-iron"),
          block.Code.Clone()
        );
        yield return (
          new AssetLocation("ppex", $"pipe-{type}-{orient}-steel"),
          block.Code.Clone()
        );
        yield return (
          new AssetLocation("smex", $"gaspipe-{type}-{orient}"),
          block.Code.Clone()
        );
      }
      // Current brick passthrough/outlet (lpex): only the smex gas-prefix swap remains. Rebuilt from
      // the variants rather than from `path`, which carries the `tier` segment the passthroughs gained
      // in M.2 and the old smex code never had.
      else if (dom == "lpex" && BrickTypes.Contains(type)) {
        string orient = block.Variant["orientation"];
        string? brick = block.Variant["brick"];
        yield return (
          new AssetLocation(
            "smex",
            brick == null
              ? $"gaspipe-{type}-{orient}"
              : $"gaspipe-{type}-{brick}-{orient}"
          ),
          block.Code.Clone()
        );
      }
    }

    // Refractory-tier bricks were removed from outlet/passthrough/passthrough-bend; fall back to fire.
    string[] refractory =
    [
      "refractorytier1",
      "refractorytier2",
      "refractorytier3",
    ];
    // The live target's code, which the passthroughs carry a `tier` segment in and the outlet does not.
    // Literals, so unlike the Clone()-based rows above they follow no variant on their own.
    static string Live(string shape, string orient) =>
      shape == "outlet"
        ? $"pipe-outlet-fire-{orient}"
        : $"pipe-{BlockPipe.CastTier}-{shape}-fire-{orient}";

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
      foreach (string brick in refractory)
        foreach (string orient in orients)
          yield return (
            new AssetLocation("smex", $"gaspipe-{shape}-{brick}-{orient}"),
            new AssetLocation("lpex", Live(shape, orient))
          );

    // Pre-brick legacy passthrough/outlet (before the brick variantgroup existed) → fire brick.
    foreach (string o in new[] { "ns", "we", "ud" })
      yield return (
        new AssetLocation("smex", $"gaspipe-passthrough-{o}"),
        new AssetLocation("lpex", Live("passthrough", o))
      );
    foreach (string o in new[] { "s", "n", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-outlet-{o}"),
        new AssetLocation("lpex", Live("outlet", o))
      );

    // Removed inline gas machines → a plain plated (iwex) straight pipe of the matching axis.
    foreach (string o in new[] { "ns", "we" })
      yield return (
        new AssetLocation("smex", $"gaspipe-blower-{o}"),
        new AssetLocation("iwex", $"pipe-plated-straight-{o}")
      );

    string[] heatedBricks =
    [
      "fire",
      "black",
      "brown",
      "cream",
      "gray",
      "orange",
      "red",
      "tan",
      "refractorytier1",
      "refractorytier2",
      "refractorytier3",
    ];
    foreach (string brick in heatedBricks)
      foreach (string o in new[] { "ns", "sn", "we", "ew" })
        yield return (
          new AssetLocation("smex", $"gaspipe-heated-{brick}-{o}"),
          new AssetLocation("iwex", $"pipe-plated-straight-{StraightAxis[o]}")
        );

    foreach (string o in new[] { "n", "s", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-intake-{o}"),
        new AssetLocation("iwex", $"pipe-plated-straight-{StraightAxis[o]}")
      );
  }
}
