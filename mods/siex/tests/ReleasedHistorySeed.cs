using System.Collections.Generic;
using ExpandedLib.Testing;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// siex's own contribution to the release-history registry: smex's shipped history. siex's
/// coverage tests (<c>ReleasedCodeCoverageTests</c>, <c>ReleasedEntityClassTests</c>) are the only
/// suite that references exlib, iiex and siex together, so they are the only runtime consumer of
/// the smex rows; registering them here keeps the harness itself free of mod-specific history.
/// </summary>
internal static class ReleasedHistorySeed {
  /// <summary>smex, every release up to and including 0.9.8 - 35 blocktypes, 351 concrete codes.</summary>
  private static readonly IReadOnlyList<ReleasedCodes.Shipped> Shipped =
  [
    new("smex", "blastfurnace/door", "smex:blastfurnacedoor", ["smex:blastfurnacedoor", "smex:blastfurnacedoor-tier1", "smex:blastfurnacedoor-tier2", "smex:blastfurnacedoor-tier3"]),
    new("smex", "blastfurnace/hopperbell", "smex:hopperbell", ["smex:hopperbell"]),
    new("smex", "blastfurnace/hopperreinforced", "smex:hopperreinforced", ["smex:hopperreinforced"]),
    new("smex", "blastfurnace/mpblower", "smex:mpblower", ["smex:mpblower-east", "smex:mpblower-north", "smex:mpblower-south", "smex:mpblower-west"]),
    new("smex", "blastfurnace/slag", "smex:slag", ["smex:slag"]),
    new("smex", "blastfurnace/solidifiediron", "smex:solidifiediron", ["smex:solidifiediron"]),
    new("smex", "blastfurnace/tap", "smex:blastfurnacetap", ["smex:blastfurnacetap-e", "smex:blastfurnacetap-n", "smex:blastfurnacetap-s", "smex:blastfurnacetap-tier1-east", "smex:blastfurnacetap-tier1-north", "smex:blastfurnacetap-tier1-south", "smex:blastfurnacetap-tier1-west", "smex:blastfurnacetap-tier2-east", "smex:blastfurnacetap-tier2-north", "smex:blastfurnacetap-tier2-south", "smex:blastfurnacetap-tier2-west", "smex:blastfurnacetap-tier3-east", "smex:blastfurnacetap-tier3-north", "smex:blastfurnacetap-tier3-south", "smex:blastfurnacetap-tier3-west", "smex:blastfurnacetap-w"]),
    new("smex", "blastfurnace/tuyere", "smex:blastfurnace", ["smex:blastfurnace-tuyere-e", "smex:blastfurnace-tuyere-n", "smex:blastfurnace-tuyere-s", "smex:blastfurnace-tuyere-tier1-e", "smex:blastfurnace-tuyere-tier1-n", "smex:blastfurnace-tuyere-tier1-s", "smex:blastfurnace-tuyere-tier1-w", "smex:blastfurnace-tuyere-tier2-e", "smex:blastfurnace-tuyere-tier2-n", "smex:blastfurnace-tuyere-tier2-s", "smex:blastfurnace-tuyere-tier2-w", "smex:blastfurnace-tuyere-tier3-e", "smex:blastfurnace-tuyere-tier3-n", "smex:blastfurnace-tuyere-tier3-s", "smex:blastfurnace-tuyere-tier3-w", "smex:blastfurnace-tuyere-w"]),
    new("smex", "converter/bessemer", "smex:converterbessemer", ["smex:converterbessemer-e", "smex:converterbessemer-east", "smex:converterbessemer-n", "smex:converterbessemer-north", "smex:converterbessemer-s", "smex:converterbessemer-south", "smex:converterbessemer-w", "smex:converterbessemer-west"]),
    new("smex", "converter/control", "smex:convertercontrol", ["smex:convertercontrol-e", "smex:convertercontrol-east", "smex:convertercontrol-n", "smex:convertercontrol-north", "smex:convertercontrol-s", "smex:convertercontrol-south", "smex:convertercontrol-w", "smex:convertercontrol-west"]),
    new("smex", "converter/intake", "smex:converter", ["smex:converter-intake-e", "smex:converter-intake-east", "smex:converter-intake-n", "smex:converter-intake-north", "smex:converter-intake-s", "smex:converter-intake-south", "smex:converter-intake-w", "smex:converter-intake-west"]),
    new("smex", "converter/transmission", "smex:convertertransmission", ["smex:convertertransmission-e", "smex:convertertransmission-east", "smex:convertertransmission-n", "smex:convertertransmission-north", "smex:convertertransmission-s", "smex:convertertransmission-south", "smex:convertertransmission-w", "smex:convertertransmission-west"]),
    new("smex", "cowperstove/heatsink", "smex:cowperstoveheatsink", ["smex:cowperstoveheatsink-e", "smex:cowperstoveheatsink-n", "smex:cowperstoveheatsink-s", "smex:cowperstoveheatsink-tier1-east", "smex:cowperstoveheatsink-tier1-north", "smex:cowperstoveheatsink-tier1-south", "smex:cowperstoveheatsink-tier1-west", "smex:cowperstoveheatsink-tier2-east", "smex:cowperstoveheatsink-tier2-north", "smex:cowperstoveheatsink-tier2-south", "smex:cowperstoveheatsink-tier2-west", "smex:cowperstoveheatsink-tier3-east", "smex:cowperstoveheatsink-tier3-north", "smex:cowperstoveheatsink-tier3-south", "smex:cowperstoveheatsink-tier3-west", "smex:cowperstoveheatsink-w"]),
    new("smex", "cowperstove/intake", "smex:cowperstove", ["smex:cowperstove-intake-tier1-east", "smex:cowperstove-intake-tier1-north", "smex:cowperstove-intake-tier1-south", "smex:cowperstove-intake-tier1-west", "smex:cowperstove-intake-tier2-east", "smex:cowperstove-intake-tier2-north", "smex:cowperstove-intake-tier2-south", "smex:cowperstove-intake-tier2-west", "smex:cowperstove-intake-tier3-east", "smex:cowperstove-intake-tier3-north", "smex:cowperstove-intake-tier3-south", "smex:cowperstove-intake-tier3-west"]),
    new("smex", "engine/airblower", "smex:engineairblower", ["smex:engineairblower-e", "smex:engineairblower-east", "smex:engineairblower-n", "smex:engineairblower-north", "smex:engineairblower-s", "smex:engineairblower-south", "smex:engineairblower-w", "smex:engineairblower-west"]),
    new("smex", "molds/toolmoldfired", "smex:toolmold", ["smex:toolmold-black-fired-doubleingot", "smex:toolmold-black-fired-plate", "smex:toolmold-black-fired-quadrod", "smex:toolmold-blue-fired-doubleingot", "smex:toolmold-blue-fired-plate", "smex:toolmold-blue-fired-quadrod", "smex:toolmold-brown-fired-doubleingot", "smex:toolmold-brown-fired-plate", "smex:toolmold-brown-fired-quadrod", "smex:toolmold-cream-fired-doubleingot", "smex:toolmold-cream-fired-plate", "smex:toolmold-cream-fired-quadrod", "smex:toolmold-earthyorange-fired-doubleingot", "smex:toolmold-earthyorange-fired-plate", "smex:toolmold-earthyorange-fired-quadrod", "smex:toolmold-fire-fired-doubleingot", "smex:toolmold-fire-fired-plate", "smex:toolmold-fire-fired-quadrod", "smex:toolmold-gray-fired-doubleingot", "smex:toolmold-gray-fired-plate", "smex:toolmold-gray-fired-quadrod", "smex:toolmold-orange-fired-doubleingot", "smex:toolmold-orange-fired-plate", "smex:toolmold-orange-fired-quadrod", "smex:toolmold-red-fired-doubleingot", "smex:toolmold-red-fired-plate", "smex:toolmold-red-fired-quadrod", "smex:toolmold-tan-fired-doubleingot", "smex:toolmold-tan-fired-plate", "smex:toolmold-tan-fired-quadrod"]),
    new("smex", "molds/toolmoldraw", "smex:toolmold", ["smex:toolmold-blue-raw-doubleingot", "smex:toolmold-blue-raw-plate", "smex:toolmold-blue-raw-quadrod", "smex:toolmold-fire-raw-doubleingot", "smex:toolmold-fire-raw-plate", "smex:toolmold-fire-raw-quadrod", "smex:toolmold-red-raw-doubleingot", "smex:toolmold-red-raw-plate", "smex:toolmold-red-raw-quadrod"]),
    new("smex", "molten/barrel", "smex:moltenbarrel", ["smex:moltenbarrel"]),
    new("smex", "molten/canalbrick/bend", "smex:moltencanal", ["smex:moltencanal-bend-black-en", "smex:moltencanal-bend-black-nw", "smex:moltencanal-bend-black-se", "smex:moltencanal-bend-black-ws", "smex:moltencanal-bend-brown-en", "smex:moltencanal-bend-brown-nw", "smex:moltencanal-bend-brown-se", "smex:moltencanal-bend-brown-ws", "smex:moltencanal-bend-cream-en", "smex:moltencanal-bend-cream-nw", "smex:moltencanal-bend-cream-se", "smex:moltencanal-bend-cream-ws", "smex:moltencanal-bend-fire-en", "smex:moltencanal-bend-fire-nw", "smex:moltencanal-bend-fire-se", "smex:moltencanal-bend-fire-ws", "smex:moltencanal-bend-gray-en", "smex:moltencanal-bend-gray-nw", "smex:moltencanal-bend-gray-se", "smex:moltencanal-bend-gray-ws", "smex:moltencanal-bend-orange-en", "smex:moltencanal-bend-orange-nw", "smex:moltencanal-bend-orange-se", "smex:moltencanal-bend-orange-ws", "smex:moltencanal-bend-red-en", "smex:moltencanal-bend-red-nw", "smex:moltencanal-bend-red-se", "smex:moltencanal-bend-red-ws", "smex:moltencanal-bend-tan-en", "smex:moltencanal-bend-tan-nw", "smex:moltencanal-bend-tan-se", "smex:moltencanal-bend-tan-ws"]),
    new("smex", "molten/canalbrick/moldpedestal", "smex:moltencanal", ["smex:moltencanal-moldpedestal-black-e", "smex:moltencanal-moldpedestal-black-n", "smex:moltencanal-moldpedestal-black-s", "smex:moltencanal-moldpedestal-black-w", "smex:moltencanal-moldpedestal-brown-e", "smex:moltencanal-moldpedestal-brown-n", "smex:moltencanal-moldpedestal-brown-s", "smex:moltencanal-moldpedestal-brown-w", "smex:moltencanal-moldpedestal-cream-e", "smex:moltencanal-moldpedestal-cream-n", "smex:moltencanal-moldpedestal-cream-s", "smex:moltencanal-moldpedestal-cream-w", "smex:moltencanal-moldpedestal-fire-e", "smex:moltencanal-moldpedestal-fire-n", "smex:moltencanal-moldpedestal-fire-s", "smex:moltencanal-moldpedestal-fire-w", "smex:moltencanal-moldpedestal-gray-e", "smex:moltencanal-moldpedestal-gray-n", "smex:moltencanal-moldpedestal-gray-s", "smex:moltencanal-moldpedestal-gray-w", "smex:moltencanal-moldpedestal-orange-e", "smex:moltencanal-moldpedestal-orange-n", "smex:moltencanal-moldpedestal-orange-s", "smex:moltencanal-moldpedestal-orange-w", "smex:moltencanal-moldpedestal-red-e", "smex:moltencanal-moldpedestal-red-n", "smex:moltencanal-moldpedestal-red-s", "smex:moltencanal-moldpedestal-red-w", "smex:moltencanal-moldpedestal-tan-e", "smex:moltencanal-moldpedestal-tan-n", "smex:moltencanal-moldpedestal-tan-s", "smex:moltencanal-moldpedestal-tan-w"]),
    new("smex", "molten/canalbrick/start", "smex:moltencanal", ["smex:moltencanal-start-black-e", "smex:moltencanal-start-black-n", "smex:moltencanal-start-black-s", "smex:moltencanal-start-black-w", "smex:moltencanal-start-brown-e", "smex:moltencanal-start-brown-n", "smex:moltencanal-start-brown-s", "smex:moltencanal-start-brown-w", "smex:moltencanal-start-cream-e", "smex:moltencanal-start-cream-n", "smex:moltencanal-start-cream-s", "smex:moltencanal-start-cream-w", "smex:moltencanal-start-fire-e", "smex:moltencanal-start-fire-n", "smex:moltencanal-start-fire-s", "smex:moltencanal-start-fire-w", "smex:moltencanal-start-gray-e", "smex:moltencanal-start-gray-n", "smex:moltencanal-start-gray-s", "smex:moltencanal-start-gray-w", "smex:moltencanal-start-orange-e", "smex:moltencanal-start-orange-n", "smex:moltencanal-start-orange-s", "smex:moltencanal-start-orange-w", "smex:moltencanal-start-red-e", "smex:moltencanal-start-red-n", "smex:moltencanal-start-red-s", "smex:moltencanal-start-red-w", "smex:moltencanal-start-tan-e", "smex:moltencanal-start-tan-n", "smex:moltencanal-start-tan-s", "smex:moltencanal-start-tan-w"]),
    new("smex", "molten/canalbrick/straight", "smex:moltencanal", ["smex:moltencanal-straight-black-ns", "smex:moltencanal-straight-black-we", "smex:moltencanal-straight-brown-ns", "smex:moltencanal-straight-brown-we", "smex:moltencanal-straight-cream-ns", "smex:moltencanal-straight-cream-we", "smex:moltencanal-straight-fire-ns", "smex:moltencanal-straight-fire-we", "smex:moltencanal-straight-gray-ns", "smex:moltencanal-straight-gray-we", "smex:moltencanal-straight-orange-ns", "smex:moltencanal-straight-orange-we", "smex:moltencanal-straight-red-ns", "smex:moltencanal-straight-red-we", "smex:moltencanal-straight-tan-ns", "smex:moltencanal-straight-tan-we"]),
    new("smex", "molten/canalbrick/tjunction", "smex:moltencanal", ["smex:moltencanal-tjunction-black-esw", "smex:moltencanal-tjunction-black-nes", "smex:moltencanal-tjunction-black-swn", "smex:moltencanal-tjunction-black-wne", "smex:moltencanal-tjunction-brown-esw", "smex:moltencanal-tjunction-brown-nes", "smex:moltencanal-tjunction-brown-swn", "smex:moltencanal-tjunction-brown-wne", "smex:moltencanal-tjunction-cream-esw", "smex:moltencanal-tjunction-cream-nes", "smex:moltencanal-tjunction-cream-swn", "smex:moltencanal-tjunction-cream-wne", "smex:moltencanal-tjunction-fire-esw", "smex:moltencanal-tjunction-fire-nes", "smex:moltencanal-tjunction-fire-swn", "smex:moltencanal-tjunction-fire-wne", "smex:moltencanal-tjunction-gray-esw", "smex:moltencanal-tjunction-gray-nes", "smex:moltencanal-tjunction-gray-swn", "smex:moltencanal-tjunction-gray-wne", "smex:moltencanal-tjunction-orange-esw", "smex:moltencanal-tjunction-orange-nes", "smex:moltencanal-tjunction-orange-swn", "smex:moltencanal-tjunction-orange-wne", "smex:moltencanal-tjunction-red-esw", "smex:moltencanal-tjunction-red-nes", "smex:moltencanal-tjunction-red-swn", "smex:moltencanal-tjunction-red-wne", "smex:moltencanal-tjunction-tan-esw", "smex:moltencanal-tjunction-tan-nes", "smex:moltencanal-tjunction-tan-swn", "smex:moltencanal-tjunction-tan-wne"]),
    new("smex", "molten/canalbrick/xjunction", "smex:moltencanal", ["smex:moltencanal-xjunction-black-nswe", "smex:moltencanal-xjunction-brown-nswe", "smex:moltencanal-xjunction-cream-nswe", "smex:moltencanal-xjunction-fire-nswe", "smex:moltencanal-xjunction-gray-nswe", "smex:moltencanal-xjunction-orange-nswe", "smex:moltencanal-xjunction-red-nswe", "smex:moltencanal-xjunction-tan-nswe"]),
    new("smex", "molten/canalcobblestone/bend", "smex:moltencanal", ["smex:moltencanal-bend-granite-en", "smex:moltencanal-bend-granite-nw", "smex:moltencanal-bend-granite-se", "smex:moltencanal-bend-granite-ws"]),
    new("smex", "molten/canalcobblestone/moldpedestal", "smex:moltencanal", ["smex:moltencanal-moldpedestal-granite-e", "smex:moltencanal-moldpedestal-granite-n", "smex:moltencanal-moldpedestal-granite-s", "smex:moltencanal-moldpedestal-granite-w"]),
    new("smex", "molten/canalcobblestone/start", "smex:moltencanal", ["smex:moltencanal-start-granite-e", "smex:moltencanal-start-granite-n", "smex:moltencanal-start-granite-s", "smex:moltencanal-start-granite-w"]),
    new("smex", "molten/canalcobblestone/straight", "smex:moltencanal", ["smex:moltencanal-straight-granite-ns", "smex:moltencanal-straight-granite-we"]),
    new("smex", "molten/canalcobblestone/tjunction", "smex:moltencanal", ["smex:moltencanal-tjunction-granite-esw", "smex:moltencanal-tjunction-granite-nes", "smex:moltencanal-tjunction-granite-swn", "smex:moltencanal-tjunction-granite-wne"]),
    new("smex", "molten/canalcobblestone/xjunction", "smex:moltencanal", ["smex:moltencanal-xjunction-granite-nswe"]),
    new("smex", "molten/tap", "smex:moltencanal", ["smex:moltencanal-tap-e", "smex:moltencanal-tap-n", "smex:moltencanal-tap-s", "smex:moltencanal-tap-w"]),
    new("smex", "slagpath", "smex:slagpath", ["smex:slagpath-free", "smex:slagpath-snow"]),
    new("smex", "slagpathslab", "smex:slagpathslab", ["smex:slagpathslab-free", "smex:slagpathslab-snow"]),
    new("smex", "slagpathstairs", "smex:slagpathstairs", ["smex:slagpathstairs-up-east-free", "smex:slagpathstairs-up-east-snow", "smex:slagpathstairs-up-north-free", "smex:slagpathstairs-up-north-snow", "smex:slagpathstairs-up-south-free", "smex:slagpathstairs-up-south-snow", "smex:slagpathstairs-up-west-free", "smex:slagpathstairs-up-west-snow"]),
    new("smex", "smokestack/intake", "smex:smokestack", ["smex:smokestack-intake-tier1-e", "smex:smokestack-intake-tier1-n", "smex:smokestack-intake-tier1-s", "smex:smokestack-intake-tier1-w", "smex:smokestack-intake-tier2-e", "smex:smokestack-intake-tier2-n", "smex:smokestack-intake-tier2-s", "smex:smokestack-intake-tier2-w", "smex:smokestack-intake-tier3-e", "smex:smokestack-intake-tier3-n", "smex:smokestack-intake-tier3-s", "smex:smokestack-intake-tier3-w"]),
  ];

  /// <summary>Every block-entity class string a released smex blocktype declared.</summary>
  private static readonly IReadOnlyList<ReleasedCodes.ShippedEntityClass> EntityClasses =
  [
    new("smex", "smex.BlockEntityBlastFurnace", ["blastfurnace/door"]),
    new("smex", "smex.BlockEntityBlastFurnaceTap", ["blastfurnace/tap"]),
    new("smex", "smex.BlockEntityConverterBessemer", ["converter/bessemer"]),
    new("smex", "smex.BlockEntityConverterControl", ["converter/control"]),
    new("smex", "smex.BlockEntityConverterTransmission", ["converter/transmission"]),
    new("smex", "smex.BlockEntityCowperStove", ["cowperstove/intake"]),
    new("smex", "smex.BlockEntityEngineAirBlower", ["engine/airblower"]),
    new("smex", "smex.BlockEntityHeatSink", ["cowperstove/heatsink"]),
    new("smex", "smex.BlockEntityMoltenBarrel", ["molten/barrel"]),
    new("smex", "smex.BlockEntityMoltenCanal", ["molten/canalbrick/bend", "molten/canalbrick/straight", "molten/canalbrick/tjunction", "molten/canalbrick/xjunction", "molten/canalcobblestone/bend", "molten/canalcobblestone/straight", "molten/canalcobblestone/tjunction", "molten/canalcobblestone/xjunction"]),
    new("smex", "smex.BlockEntityMoltenCanalMoldPedestal", ["molten/canalbrick/moldpedestal", "molten/canalcobblestone/moldpedestal"]),
    new("smex", "smex.BlockEntityMoltenCanalStart", ["molten/canalbrick/start", "molten/canalcobblestone/start"]),
    new("smex", "smex.BlockEntityMoltenCanalTap", ["molten/tap"]),
    new("smex", "smex.BlockEntityMpBlower", ["blastfurnace/mpblower"]),
    new("smex", "smex.BlockEntitySlag", ["blastfurnace/slag"]),
    new("smex", "smex.BlockEntitySmokeStack", ["smokestack/intake"]),
    new("smex", "smex.BlockEntitySolidifiedIron", ["blastfurnace/solidifiediron"]),
    new("smex", "smex.BlockEntityTuyere", ["blastfurnace/tuyere"]),
  ];

  /// <summary>smex codes with no path to a live block - see <see cref="ExpandedLib.Testing.ReleasedCodeDebt"/>.</summary>
  private static readonly IReadOnlyList<string> Debt =
  [
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

  internal static void Register() =>
    ReleasedHistory.Register(
      "siex",
      Shipped,
      EntityClasses,
      new Dictionary<string, string> { ["smex"] = "0.9.8" },
      Debt
    );
}
