using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Migrates placed pipes across two moves onto the current tier-split blocks. The pipe base lost its
/// iron/steel <c>material</c> axis, so <c>ppex:pipe-{segment}-{orient}-{iron|steel}</c> becomes
/// <c>iiex:pipe-plated-{type}-{orient}</c> and the valves <c>iiex:pipe-cast-{type}-{orient}</c>; the older
/// smex→ppex move (<c>gaspipe-* → pipe-*</c>) lands on those same blocks. Remaps derive from the
/// registered blocks, so orientation lists stay in sync. The flat rename is
/// <see cref="PpexRenameMigration"/>; see docs/design/machines/cast-pipes.md for the tier model.
/// <para>
/// The branches below discriminate on the block's own <c>tier</c> variant, never on its domain, which
/// the merge collapsed. Both tiers carry the same <see cref="SegmentTypes"/>, so a domain gate would
/// emit every released segment code twice with different targets - a free tier upgrade.
/// </para>
/// </summary>
public class PipeMigration : IBlockCodeMigration {
  /// <summary>The domain the live pipe blocks carry. A literal, matching the frozen left-hand
  /// domains below: both halves of a remap pair are historical facts once written.</summary>
  private const string Domain = "iiex";

  public string Name =>
    "Pipe tiers: material axis dropped, tier added to the code";

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

      string path = block.Code.Path;
      string? type = block.Variant["type"];
      string? tier = block.Variant["tier"];
      // The domain still scopes the walk to our own blocks - hpex's rolled tier rides the same
      // registry and must not be claimed here. What it no longer does is pick the TIER.
      if (
        block.Code.Domain != Domain
        || type == null
        || !path.StartsWith("pipe-")
      )
        continue;

      // Current plated segments: old material-suffixed ppex codes and the older smex gaspipe. ppex
      // shipped one pipe family and it lands on plated, the tier a player reaches first - never on
      // cast, which would hand every released run a free upgrade.
      if (tier == BlockPipe.PlatedTier && SegmentTypes.Contains(type)) {
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
      // Current cast valves: old material-suffixed ppex codes and the older smex gaspipe. The valves
      // only ever existed at the cast tier, so unlike the segments there is nothing to disambiguate.
      else if (tier == BlockPipe.CastTier && ValveTypes.Contains(type)) {
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
      // Current brick passthrough/outlet: only the smex gas-prefix swap remains. Rebuilt from the
      // variants rather than from `path`, which carries the `tier` segment the passthroughs gained in
      // M.2 and the old smex code never had. The passthroughs are cast-tier and the outlet carries no
      // tier axis at all, so the gate has to admit both rather than name one tier.
      else if (
        (tier == BlockPipe.CastTier || tier == null)
        && BrickTypes.Contains(type)
      ) {
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
            new AssetLocation(Domain, Live(shape, orient))
          );

    // Pre-brick legacy passthrough/outlet (before the brick variantgroup existed) → fire brick.
    foreach (string o in new[] { "ns", "we", "ud" })
      yield return (
        new AssetLocation("smex", $"gaspipe-passthrough-{o}"),
        new AssetLocation(Domain, Live("passthrough", o))
      );
    foreach (string o in new[] { "s", "n", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-outlet-{o}"),
        new AssetLocation(Domain, Live("outlet", o))
      );

    // Removed inline gas machines → a plain plated (iwex) straight pipe of the matching axis.
    foreach (string o in new[] { "ns", "we" })
      yield return (
        new AssetLocation("smex", $"gaspipe-blower-{o}"),
        new AssetLocation(Domain, $"pipe-plated-straight-{o}")
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
          new AssetLocation(Domain, $"pipe-plated-straight-{StraightAxis[o]}")
        );

    foreach (string o in new[] { "n", "s", "w", "e" })
      yield return (
        new AssetLocation("smex", $"gaspipe-intake-{o}"),
        new AssetLocation(Domain, $"pipe-plated-straight-{StraightAxis[o]}")
      );
  }
}
