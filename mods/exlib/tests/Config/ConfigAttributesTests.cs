using ExpandedLib.Config;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The attribute types themselves - <see cref="ExConfigRangeAttribute"/> and
/// <see cref="ExConfigRegisterAttribute"/> - plus the two carry-over paths <see cref="ConfigEditTests"/>
/// does not reach: <see cref="ExConfigRegister{TConfig}.LegacySectionIds"/> folding a section this mod
/// used to be keyed under, and <see cref="ExConfigProfiles"/>'s own lookup. The numeric range
/// enforcement itself (both edges, the baseline non-negative default) is <see cref="ConfigEditTests"/>'
/// job over <c>EditableConfig.Ratio</c>; this file does not repeat it.
/// </summary>
public class ConfigAttributesTests {
  #region ExConfigRangeAttribute

  [Fact]
  public void A_two_argument_range_carries_both_bounds() {
    var attr = new ExConfigRangeAttribute(0, 1);

    Assert.Equal(0, attr.Min);
    Assert.Equal(1, attr.Max);
  }

  [Fact]
  public void A_one_argument_range_is_a_floor_with_no_upper_bound() {
    var attr = new ExConfigRangeAttribute(5);

    Assert.Equal(5, attr.Min);
    Assert.Equal(double.PositiveInfinity, attr.Max);
  }

  #endregion

  #region ExConfigRegisterAttribute

  [Fact]
  public void Constructor_arguments_become_FileName_and_ModId() {
    var attr = new ExConfigRegisterAttribute("ex_values.json", "iiex");

    Assert.Equal("ex_values.json", attr.FileName);
    Assert.Equal("iiex", attr.ModId);
  }

  [Fact]
  public void Optional_properties_default_to_unset() {
    var attr = new ExConfigRegisterAttribute("ex_values.json", "iiex");

    Assert.Null(attr.AccessorName);
    Assert.Null(attr.LegacyFileNames);
    Assert.Null(attr.LegacySectionIds);
    Assert.False(attr.Manageable);
  }

  [Fact]
  public void Optional_properties_carry_the_value_they_are_set_to() {
    var attr = new ExConfigRegisterAttribute("ex_values.json", "iiex") {
      AccessorName = "IiexValues",
      LegacyFileNames = ["ex_iiex.json"],
      LegacySectionIds = ["oldiiex"],
      Manageable = true,
    };

    Assert.Equal("IiexValues", attr.AccessorName);
    Assert.Equal(["ex_iiex.json"], attr.LegacyFileNames);
    Assert.Equal(["oldiiex"], attr.LegacySectionIds);
    Assert.True(attr.Manageable);
  }

  #endregion

  #region LegacySectionIds fold (ExConfigRegister.Load)

  private sealed class SectionConfig : IExVersionedConfig {
    public string? ConfigVersion { get; set; }
    public int Value { get; set; } = 1;
  }

  private static ICoreAPI FakeApiWithDocument(JObject doc) {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>(Arg.Any<string>()).Returns(doc);

    var mod = Substitute.For<Mod>();
    typeof(Mod).GetProperty("Info")!.SetValue(mod, new ModInfo { Version = "1.0.0" });
    var modLoader = Substitute.For<IModLoader>();
    modLoader.GetMod("newmod").Returns(mod);
    api.ModLoader.Returns(modLoader);
    return api;
  }

  [Fact]
  public void A_legacy_sections_values_are_carried_into_the_new_section() {
    // "oldmod"'s section exists, "newmod"'s does not - the rename-or-absorb case.
    var doc = new JObject {
      ["oldmod"] = JObject.FromObject(new SectionConfig { Value = 42 }),
    };
    var store = new ExConfigRegister<SectionConfig>("shared.json", "newmod") {
      LegacySectionIds = ["oldmod"],
    };

    store.Load(FakeApiWithDocument(doc));

    Assert.Equal(42, store.Config.Value);
  }

  [Fact]
  public void A_legacy_section_only_fills_gaps_when_the_new_section_already_has_one() {
    // Both sections exist: the survivor (the mod's own current section) wins.
    var doc = new JObject {
      ["oldmod"] = JObject.FromObject(new SectionConfig { Value = 42 }),
      ["newmod"] = JObject.FromObject(new SectionConfig { Value = 7 }),
    };
    var store = new ExConfigRegister<SectionConfig>("shared.json", "newmod") {
      LegacySectionIds = ["oldmod"],
    };

    store.Load(FakeApiWithDocument(doc));

    Assert.Equal(7, store.Config.Value);
  }

  [Fact]
  public void With_no_legacy_section_ids_nothing_is_folded() {
    var doc = new JObject {
      ["oldmod"] = JObject.FromObject(new SectionConfig { Value = 42 }),
    };
    var store = new ExConfigRegister<SectionConfig>("shared.json", "newmod");

    store.Load(FakeApiWithDocument(doc));

    Assert.Equal(1, store.Config.Value); // coded default - the "oldmod" section was left alone
  }

  #endregion

  #region ExConfigProfiles

  private static string FreshModId() => "profilestest-" + System.Guid.NewGuid().ToString("N")[..8];

  [Fact]
  public void A_registered_config_is_found_by_its_mod_id() {
    string modId = FreshModId();
    var store = new ExConfigRegister<SectionConfig>("shared.json", modId);

    ExConfigProfiles.Register(store);

    Assert.True(ExConfigProfiles.TryGet(modId, out var found));
    Assert.Same(store, found);
    Assert.Contains(modId, ExConfigProfiles.Codes);
  }

  [Fact]
  public void An_unregistered_mod_id_is_not_found() {
    Assert.False(ExConfigProfiles.TryGet(FreshModId(), out _));
  }

  #endregion
}
