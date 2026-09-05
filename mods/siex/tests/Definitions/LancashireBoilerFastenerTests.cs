using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.Items;
using SteelIndustryExpanded.BlockStructures.Boiler.Blocks;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The Lancashire boiler holds more pressure than the Cornish, so the gate applies to it just as hard.
/// Its rivets are iiex's: a rivet is a rivet whatever the shell is made of, and siex mints none.
/// See <see cref="PressureVesselGate"/>.
/// </summary>
public class LancashireBoilerFastenerTests {
  private static ExBlockDef Boiler =>
    BlockBoilerLancashire.Definitions("siex").Single();

  private const string Rivet = "iiex:" + FastenerItemDefinitions.RivetCode;

  [Fact]
  public void The_lancashire_boiler_is_riveted_and_never_nailed() =>
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

  /// <summary>Mass-neutral, as the Cornish is: 48 bundles at 12.5 u is the 600 u its 24 nail bundles
  /// cost.</summary>
  [Fact]
  public void The_rivets_cost_what_the_nails_did() {
    const int WasNailBundles = 24;
    const int VanillaBundleUnits = 25;

    int rivets = PressureVesselGate.RivetsRequired(Boiler, Rivet);

    Assert.Equal(
      WasNailBundles * VanillaBundleUnits,
      rivets * FastenerItemDefinitions.RivetUnits,
      3
    );
  }
}
