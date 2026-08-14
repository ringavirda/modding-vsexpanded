using IronIndustryExpanded;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The config-driven burden-grade classifier: how a mix's flux fraction maps to a named grade through the
/// <see cref="IiexConfig.BurdenProfiles"/> band list, and that retuning the config re-grades the same mix.
/// Burden is ore and flux, and flux is the only quality it carries.
/// <para>
/// The shipped bands tile 0..1 with no gap, so <c>offspec</c> is reachable only through the edited-config
/// case below.
/// </para>
/// </summary>
public class BurdenProfileTests {
  #region Default grades

  [Theory]
  [InlineData(0f, 0f, "iiex:burden-profile-empty")]
  // Boundaries belong to the earlier band: the list is scanned in order with both ends inclusive, so
  // 3 % reads under-fluxed and 8 % reads standard. Stated as cases because a re-sort of that list would
  // change which side of the line a boundary falls on.
  [InlineData(99f, 1f, "iiex:burden-profile-underfluxed")]
  [InlineData(97f, 3f, "iiex:burden-profile-underfluxed")]
  [InlineData(96f, 4f, "iiex:burden-profile-standard")]
  [InlineData(95f, 5f, "iiex:burden-profile-standard")]
  [InlineData(92f, 8f, "iiex:burden-profile-standard")]
  [InlineData(90f, 10f, "iiex:burden-profile-overfluxed")]
  [InlineData(50f, 50f, "iiex:burden-profile-overfluxed")]
  public void Default_bands_classify_by_flux_fraction(
    float iron,
    float flux,
    string expected
  ) {
    Assert.Equal(
      expected,
      Burden.ProfileLangKey(new BurdenMix(iron, flux, 0f))
    );
  }

  [Fact]
  public void A_stamped_fuel_part_cannot_change_the_grade() {
    // `BurdenMix.Fuel` exists for the shaft's own composition read, so a burden stack carrying a stray
    // fuel part is representable. It must not move the grade, or there are two answers to how much
    // carbon is at the raceway.
    //
    // Fuel does dilute the flux fraction, which is arithmetic rather than grading, so both mixes below
    // hold flux at 5 % of the total and the only thing varying is whether a fuel part exists.
    Assert.Equal(
      Burden.ProfileLangKey(new BurdenMix(95f, 5f, 0f)),
      Burden.ProfileLangKey(new BurdenMix(45f, 5f, 50f))
    );
  }

  [Fact]
  public void Two_batches_at_the_same_ratio_read_the_same_grade_and_merge() {
    // Two stacks of the same mix must carry identical attribute sets, or they read as different grades
    // and refuse to stack. See docs/design/items/burden.md, Gotcha 2.
    //
    // Asserted through `Write` rather than by hand-setting attributes: the claim is about what the one
    // writer produces, so a fixture that stamped attributes itself would test its own arithmetic.
    var mix = new BurdenMix(190f, 10f, 0f);
    ItemStack first = TestStack();
    ItemStack second = TestStack();
    Burden.Write(first, mix);
    Burden.Write(second, mix);

    Assert.Equal(
      Burden.ProfileLangKey(mix),
      Burden.ProfileLangKey(Burden.Read(first))
    );
    Assert.Equal(
      Burden.ProfileLangKey(Burden.Read(first)),
      Burden.ProfileLangKey(Burden.Read(second))
    );
    // Indistinguishable attributes are what let them stack in a hopper.
    Assert.Equal(Burden.Read(first), Burden.Read(second));
    Assert.True(first.Attributes.Equals(null!, second.Attributes));
  }

  private static ItemStack TestStack() =>
    new(new Item { Code = new AssetLocation("iiex:burden"), ItemId = 9100 }, 1);

  #endregion

  #region Config-driven

  [Fact]
  public void Retuning_the_config_bands_regrades_the_same_mix() {
    var original = IiexValues.BurdenProfiles;
    try {
      IiexValues.Edit(c =>
        c.BurdenProfiles = [new() { Key = "heavyflux", MinFlux = 0.4f }]
      );

      // 10 % flux matches no band (the custom one needs >= 40 %), so the mix is off-spec.
      Assert.Equal(
        "iiex:burden-profile-offspec",
        Burden.ProfileLangKey(new BurdenMix(90f, 10f, 0f))
      );
      // 50 % flux hits the single custom grade.
      Assert.Equal(
        "iiex:burden-profile-heavyflux",
        Burden.ProfileLangKey(new BurdenMix(50f, 50f, 0f))
      );
    } finally {
      IiexValues.Edit(c => c.BurdenProfiles = original);
    }
  }

  #endregion
}
