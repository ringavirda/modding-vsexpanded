using IronworkingExpanded;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The config-driven burden-grade classifier: how a mix's <b>flux</b> fraction maps to a named grade via
/// the <see cref="IwexConfig.BurdenProfiles"/> band list, and that retuning the config re-grades the same
/// mix.
/// <para>
/// <b>Every case here once graded by</b> the <em>coke</em> fraction, against
/// four bands (<c>burnedout</c> / <c>lowcoke</c> / <c>standard</c> / <c>highcoke</c>) and a derived
/// <c>lowflux</c> floor. Coke left the burden when charging became layered - it is charged as its own
/// bands at the furnace and metered at the raceway - so those cases graded a number nothing reads. Burden
/// is ore and flux, and flux is the only quality it carries.
/// </para>
/// <para>
/// The bands tile 0..1 with no gap, so <c>offspec</c> is unreachable on the shipped config. It survives
/// for the edited-config case below, which is the only way to open a hole.
/// </para>
/// </summary>
public class BurdenProfileTests
{
  #region Default grades

  [Theory]
  [InlineData(0f, 0f, "iwex:burden-profile-empty")]
  // The boundaries belong to the earlier band - the list is scanned in order and both ends are
  // inclusive - so 3 % reads under-fluxed and 8 % reads standard. Stated as cases because "which side of
  // the line" is exactly what a re-sort of that list would silently change.
  [InlineData(99f, 1f, "iwex:burden-profile-underfluxed")]
  [InlineData(97f, 3f, "iwex:burden-profile-underfluxed")]
  [InlineData(96f, 4f, "iwex:burden-profile-standard")]
  [InlineData(95f, 5f, "iwex:burden-profile-standard")]
  [InlineData(92f, 8f, "iwex:burden-profile-standard")]
  [InlineData(90f, 10f, "iwex:burden-profile-overfluxed")]
  [InlineData(50f, 50f, "iwex:burden-profile-overfluxed")]
  public void Default_bands_classify_by_flux_fraction(
    float iron,
    float flux,
    string expected
  )
  {
    Assert.Equal(expected, Burden.ProfileLangKey(new BurdenMix(iron, flux, 0f)));
  }

  [Fact]
  public void A_stamped_fuel_part_cannot_change_the_grade()
  {
    // The load-bearing case of the collapse. `BurdenMix.Fuel` still exists - the shaft's own
    // composition read uses it for fuel bands (kept deliberately) - so a burden stack
    // carrying a stray fuel part is representable. It must not move the grade, or the two answers to
    // "how much carbon is at the raceway" come back.
    //
    // Fuel does dilute the flux fraction, which is arithmetic rather than grading: 5 flux in 100 is
    // standard, and the same 5 flux beside 900 fuel is not. Both mixes below hold flux at 5 % of the
    // total, so the only thing varying is whether a fuel part exists at all.
    Assert.Equal(
      Burden.ProfileLangKey(new BurdenMix(95f, 5f, 0f)),
      Burden.ProfileLangKey(new BurdenMix(45f, 5f, 50f))
    );
  }

  [Fact]
  public void Two_batches_at_the_same_ratio_read_the_same_grade_and_merge()
  {
    // Pins closed the defect `docs/design/items/burden.md` Gotcha 2 recorded: two stamping
    // conventions. Burden written before the `coke` → `fuel` rename kept its carbon under a legacy `coke`
    // attribute, and `Burden.Read` fell back to it - so two stacks of the same material could carry
    // different attribute sets, read as different grades, and refuse to stack. The mixer is deleted and
    // the burdenmaker is the only writer left, so the fallback was removed and nothing can be
    // stamped the old way again.
    //
    // Asserted through `Write`, not by hand-setting attributes: the claim is about what the one writer
    // produces, so a fixture that stamped attributes itself would be testing its own arithmetic.
    var mix = new BurdenMix(190f, 10f, 0f);
    ItemStack first = TestStack();
    ItemStack second = TestStack();
    Burden.Write(first, mix);
    Burden.Write(second, mix);

    Assert.Equal(Burden.ProfileLangKey(mix), Burden.ProfileLangKey(Burden.Read(first)));
    Assert.Equal(
      Burden.ProfileLangKey(Burden.Read(first)),
      Burden.ProfileLangKey(Burden.Read(second))
    );
    // ...and they really are indistinguishable, which is what makes them stack in a hopper.
    Assert.Equal(Burden.Read(first), Burden.Read(second));
    Assert.True(first.Attributes.Equals(null!, second.Attributes));
  }

  private static ItemStack TestStack() =>
    new(new Item { Code = new AssetLocation("iwex:burden"), ItemId = 9100 }, 1);

  #endregion

  #region Config-driven

  [Fact]
  public void Retuning_the_config_bands_regrades_the_same_mix()
  {
    var original = IwexValues.BurdenProfiles;
    try
    {
      IwexValues.Edit(c =>
        c.BurdenProfiles = [new() { Key = "heavyflux", MinFlux = 0.4f }]
      );

      // 10 % flux no longer matches anything (the custom band needs >= 40 %) -> off-spec...
      Assert.Equal(
        "iwex:burden-profile-offspec",
        Burden.ProfileLangKey(new BurdenMix(90f, 10f, 0f))
      );
      // ...while 50 % flux now hits the single custom grade.
      Assert.Equal(
        "iwex:burden-profile-heavyflux",
        Burden.ProfileLangKey(new BurdenMix(50f, 50f, 0f))
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BurdenProfiles = original);
    }
  }

  #endregion
}
