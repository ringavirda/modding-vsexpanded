using System.Collections.Generic;

namespace ExpandedLib.Testing;

/// <summary>
/// Released block codes that reach no live block today - the migration debt of ppex 0.6.8 and smex
/// 0.9.8, recorded 2026-08-14 so the coverage guard still fails on anything new. It was invisible
/// until <see cref="ReleasedCodes"/> was refreshed off 0.6.4/0.9.4, which is why it accumulated.
/// <para>
/// Most of it is one failure repeated: a <c>side</c> group that moved to
/// <c>loadFromProperties: abstract/horizontalorientation</c> ships vanilla's full words where the
/// live definition renders letters. The rest are blocktypes 0.9.8 added outright. Owner questions
/// and the full reasoning are in docs/internal/plans/STATE.md, B25.
/// </para>
/// <para>
/// This list may only shrink: paying a row off means writing its migration and deleting it here.
/// </para>
/// </summary>
public static class ReleasedCodeDebt {
  /// <summary>Released codes with no path to a live block, grouped by the blocktype they came from.</summary>
  public static readonly IReadOnlyList<string> KnownUnmigrated =
  [
    // ppex:mpfluidpump
    "ppex:mpfluidpump-east",
    "ppex:mpfluidpump-north",
    "ppex:mpfluidpump-south",
    "ppex:mpfluidpump-west",
    // smex:blastfurnace
    "smex:blastfurnace-tuyere-tier1-e",
    "smex:blastfurnace-tuyere-tier1-n",
    "smex:blastfurnace-tuyere-tier1-s",
    "smex:blastfurnace-tuyere-tier1-w",
    "smex:blastfurnace-tuyere-tier2-e",
    "smex:blastfurnace-tuyere-tier2-n",
    "smex:blastfurnace-tuyere-tier2-s",
    "smex:blastfurnace-tuyere-tier2-w",
    "smex:blastfurnace-tuyere-tier3-e",
    "smex:blastfurnace-tuyere-tier3-n",
    "smex:blastfurnace-tuyere-tier3-s",
    "smex:blastfurnace-tuyere-tier3-w",
    // smex:blastfurnacetap
    "smex:blastfurnacetap-tier1-east",
    "smex:blastfurnacetap-tier1-north",
    "smex:blastfurnacetap-tier1-south",
    "smex:blastfurnacetap-tier1-west",
    "smex:blastfurnacetap-tier2-east",
    "smex:blastfurnacetap-tier2-north",
    "smex:blastfurnacetap-tier2-south",
    "smex:blastfurnacetap-tier2-west",
    "smex:blastfurnacetap-tier3-east",
    "smex:blastfurnacetap-tier3-north",
    "smex:blastfurnacetap-tier3-south",
    "smex:blastfurnacetap-tier3-west",
    // smex:converter
    "smex:converter-intake-east",
    "smex:converter-intake-north",
    "smex:converter-intake-south",
    "smex:converter-intake-west",
    // smex:converterbessemer
    "smex:converterbessemer-east",
    "smex:converterbessemer-north",
    "smex:converterbessemer-south",
    "smex:converterbessemer-west",
    // smex:convertercontrol
    "smex:convertercontrol-east",
    "smex:convertercontrol-north",
    "smex:convertercontrol-south",
    "smex:convertercontrol-west",
    // smex:convertertransmission
    "smex:convertertransmission-east",
    "smex:convertertransmission-north",
    "smex:convertertransmission-south",
    "smex:convertertransmission-west",
    // smex:cowperstove
    "smex:cowperstove-intake-tier1-east",
    "smex:cowperstove-intake-tier1-north",
    "smex:cowperstove-intake-tier1-south",
    "smex:cowperstove-intake-tier1-west",
    "smex:cowperstove-intake-tier2-east",
    "smex:cowperstove-intake-tier2-north",
    "smex:cowperstove-intake-tier2-south",
    "smex:cowperstove-intake-tier2-west",
    "smex:cowperstove-intake-tier3-east",
    "smex:cowperstove-intake-tier3-north",
    "smex:cowperstove-intake-tier3-south",
    "smex:cowperstove-intake-tier3-west",
    // smex:cowperstoveheatsink
    "smex:cowperstoveheatsink-tier1-east",
    "smex:cowperstoveheatsink-tier1-north",
    "smex:cowperstoveheatsink-tier1-south",
    "smex:cowperstoveheatsink-tier1-west",
    "smex:cowperstoveheatsink-tier2-east",
    "smex:cowperstoveheatsink-tier2-north",
    "smex:cowperstoveheatsink-tier2-south",
    "smex:cowperstoveheatsink-tier2-west",
    "smex:cowperstoveheatsink-tier3-east",
    "smex:cowperstoveheatsink-tier3-north",
    "smex:cowperstoveheatsink-tier3-south",
    "smex:cowperstoveheatsink-tier3-west",
    // smex:engineairblower
    "smex:engineairblower-east",
    "smex:engineairblower-north",
    "smex:engineairblower-south",
    "smex:engineairblower-west",
  ];
}
