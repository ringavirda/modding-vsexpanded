using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// Rewrites the molten-network and blast-furnace blocks that moved from the <c>smex</c> domain to
/// <c>iwex</c>. Each row of <c>Relocated</c> names the code as it shipped in smex 0.9.4 and the code it
/// lives under now; variants are carried across from the live registry, so a new orientation needs no
/// edit here. <c>hopperbell</c> and <c>hopperreinforced</c> stayed in smex, so they have no row.
/// <para>
/// Relocated blocks that carry a block entity (the canals' charge, the tap, the mold pedestal) also get
/// their saved tree copied verbatim: old and new are the same class, so the state maps one-to-one. Block
/// instances held as item stacks are rewritten by the framework.
/// </para>
/// </summary>
public class SmexToIiexMigration : IBlockCodeMigration, IBlockEntityMigration {
  public string Name => "Molten network + blast furnace moved smex -> iwex";

  /// <summary>
  /// <c>(smex base code as shipped, current iwex base code)</c>. The left side never changes; the right
  /// side follows any later iwex rename.
  /// </summary>
  private static readonly (string Old, string New)[] Relocated =
  [
    // canalbrick/cobblestone × start/straight/bend/t/x-junction/moldpedestal/tap.
    ("moltencanal", "molten-canal"),
    ("slag", "slag-block"),
    // The two frozen-melt blocks merged into one variant-grouped block. Every block smex shipped was a
    // blast furnace's residue, so pig iron is the correct target variant.
    ("solidifiediron", "hearthmetal-pigiron"),
    // Narrowed to the tuyere itself: a broader ("blastfurnace", "furnace") row would walk every live
    // iwex:furnace-* block and invent smex sources for parts that never shipped.
    ("blastfurnace-tuyere", "furnace-tuyere"),
    // smex shipped one tap blocktype for both notches, so the old code carries no record of which a
    // given block was. Every tap therefore comes back as the iron tap, and one built as a slag tap must
    // be re-crafted.
    ("blastfurnacetap", "furnace-irontap"),
    // The slag-paving line, which shipped in smex 0.9.8 and stays in the iron loop. It carried no row
    // while ReleasedCodes was derived from 0.9.4 and so did not know these codes existed; the cover
    // and stair variant grammars are unchanged, so the suffixes carry straight across.
    ("slagpath", "slag-path"),
    ("slagpathslab", "slag-pathslab"),
    ("slagpathstairs", "slag-pathstairs"),
    // "moltenbarrel" is absent on purpose: its code gained a construction variant, so
    // BarrelConstructionMigration maps smex:moltenbarrel straight to iwex:molten-barrel-plated in one
    // hop rather than through the dead iwex:moltenbarrel this table would produce.
  ];

  /// <summary>
  /// The MP blower, which shipped in smex 0.9.8 as <c>mpblower</c> and is now the furnace's twin-tub
  /// blower. Paired code by code rather than through <c>Relocated</c> because both the spelling and the
  /// group name changed: it shipped a property-sourced <c>side</c>, which renders vanilla's full words,
  /// where the live block declares an <c>orientation</c> of single letters. <c>legacySideWords</c> reads
  /// the <c>side</c> variant and so cannot bridge that; naming the four pairs keeps each blower facing
  /// the way it was built.
  /// </summary>
  private static readonly (string Old, string New)[] BlowerFacings =
  [
    ("mpblower-north", "iiex:furnace-twintubblower-n"),
    ("mpblower-east", "iiex:furnace-twintubblower-e"),
    ("mpblower-south", "iiex:furnace-twintubblower-s"),
    ("mpblower-west", "iiex:furnace-twintubblower-w"),
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach (var (old, @new) in Relocated)
      foreach (var pair in CodeRelocation.Remap(api, "smex", old, "iiex", @new))
        yield return pair;

    // The furnace door shipped with no variant group; iwex's charge door has a `side`. The old code
    // cannot say which way it faced, so every one comes back facing north and may need wrenching.
    // smex 0.9.8 added a `refractory` tier to the same block, and those spellings are in worlds too;
    // the tier carried no meaning the charge door keeps, so all four land on the same block.
    foreach (
      string old in (string[])
        [
          "blastfurnacedoor",
          "blastfurnacedoor-tier1",
          "blastfurnacedoor-tier2",
          "blastfurnacedoor-tier3",
        ]
    )
      foreach (
        var pair in CodeRelocation.RemapToDefault(
          api,
          "smex",
          old,
          "iiex:furnace-chargedoor-n"
        )
      )
        yield return pair;

    foreach (var (old, @new) in BlowerFacings)
      foreach (var pair in CodeRelocation.RemapToDefault(api, "smex", old, @new))
        yield return pair;
  }

  /// <summary>Copies the relocated block entity's saved state verbatim - same class, identical tree.</summary>
  public void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  ) {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
