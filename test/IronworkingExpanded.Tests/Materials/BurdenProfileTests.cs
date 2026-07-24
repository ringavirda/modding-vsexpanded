using IronworkingExpanded;
using IronworkingExpanded.Items;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The config-driven burden-grade classifier: how iron/flux/coke fractions map to a named grade via
/// the <see cref="IwexConfig.BurdenProfiles"/> band list, the off-spec catch-all for mixes outside
/// every band, and that retuning the config re-grades the same mix.
/// </summary>
public class BurdenProfileTests
{
  #region Default grades

  [Theory]
  [InlineData(0f, 0f, 0f, "iwex:burden-profile-empty")]
  [InlineData(80f, 10f, 10f, "iwex:burden-profile-lowcoke")]
  [InlineData(70f, 10f, 20f, "iwex:burden-profile-standard")]
  [InlineData(60f, 10f, 30f, "iwex:burden-profile-highcoke")]
  [InlineData(80f, 0f, 20f, "iwex:burden-profile-lowflux")] // no flux at all -> flux shortfall
  [InlineData(90f, 3f, 7f, "iwex:burden-profile-lowflux")] // 3% flux, under the 5% floor
  public void Default_bands_classify_by_coke_fraction_gated_on_flux(
    float iron,
    float flux,
    float coke,
    string expected
  )
  {
    Assert.Equal(
      expected,
      Burden.ProfileLangKey(new BurdenMix(iron, flux, coke))
    );
  }

  #endregion

  #region Config-driven

  [Fact]
  public void Retuning_the_config_bands_regrades_the_same_mix()
  {
    var original = IwexValues.BurdenProfiles;
    try
    {
      IwexValues.Edit(c =>
        c.BurdenProfiles = [new() { Key = "richcoke", MinFuel = 0.4f }]
      );

      // 30% fuel no longer matches anything (the custom band needs >= 40%) -> off-spec...
      Assert.Equal(
        "iwex:burden-profile-offspec",
        Burden.ProfileLangKey(new BurdenMix(60f, 10f, 30f))
      );
      // ...while 50% coke now hits the single custom grade.
      Assert.Equal(
        "iwex:burden-profile-richcoke",
        Burden.ProfileLangKey(new BurdenMix(40f, 10f, 50f))
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BurdenProfiles = original);
    }
  }

  #endregion
}
