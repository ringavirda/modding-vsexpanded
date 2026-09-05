using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using IronIndustryExpanded.Items;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The Cornish boiler is the first thing in the game that holds pressure, and it is where the rivet
/// arrives. See <see cref="PressureVesselGate"/> for why this is asserted as a negative.
/// </summary>
public class CornishBoilerFastenerTests {
  private static ExBlockDef Boiler =>
    BlockBoilerCornish.Definitions("iiex").Single();

  private const string Rivet = "iiex:" + FastenerItemDefinitions.RivetCode;

  [Fact]
  public void The_cornish_boiler_is_riveted_and_never_nailed() =>
    AssertRivetedOnly();

  private static void AssertRivetedOnly() {
    // The premise first: the boiler is construction-staged and does require something. A block whose
    // stages stopped resolving would pass the negative below while proving nothing.
    Assert.NotEmpty(PressureVesselGate.StageIngredients(Boiler));

    IReadOnlyList<string> nailed = PressureVesselGate.NailedIngredients(Boiler);
    Assert.True(
      nailed.Count == 0,
      "a pressure vessel accepts nails: " + string.Join(", ", nailed)
    );
    Assert.True(
      PressureVesselGate.RivetsRequired(Boiler, Rivet) > 0,
      "the boiler requires no rivets at all"
    );
  }

  /// <summary>
  /// The move to rivets was mass-neutral: 32 bundles at 12.5 u is the 400 u the 16 nail bundles cost. The
  /// gate is the change, not the price - a boiler that got cheaper for being riveted would make the rivet
  /// route a reward rather than a requirement.
  /// </summary>
  [Fact]
  public void The_rivets_cost_what_the_nails_did() {
    const int WasNailBundles = 16;
    const int VanillaBundleUnits = 25;

    int rivets = PressureVesselGate.RivetsRequired(Boiler, Rivet);

    Assert.Equal(
      WasNailBundles * VanillaBundleUnits,
      rivets * FastenerItemDefinitions.RivetUnits,
      3
    );
  }
}
