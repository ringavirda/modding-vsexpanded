using System.Collections.Generic;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="PreferenceRegistry"/> discovery and <see cref="ExPreferences"/>'s per-player store:
/// registration finds a decorated preference, a player's choice round-trips through the config file
/// double, and an unknown key degrades to the preference's own default rather than throwing.
/// </summary>
public class PreferencesTests {
  private const string Key = "prefstest";

  [PreferenceRegister]
  private sealed class TestPreference : IExPreference {
    public string Key => PreferencesTests.Key;
    public string[] Applied = [];
    public IReadOnlyList<string> Options { get; } = ["a", "b"];
    public string Default => "a";
    public void Apply(string value) => Applied = [value];
  }

  #region PreferenceRegistry.RegisterAll

  [Fact]
  public void RegisterAll_finds_a_decorated_preference_in_the_assembly() {
    var api = new TestWorld().Api;
    var mod = FakeMod();

    PreferenceRegistry.RegisterAll(api, mod, typeof(TestPreference).Assembly);

    Assert.IsType<TestPreference>(ExPreferences.Find(Key));
  }

  #endregion

  #region ExPreferences round-trip

  [Fact]
  public void A_players_choice_persists_across_a_fresh_LoadConfig() {
    var world = new TestWorld();
    PreferenceRegistry.RegisterAll(world.Api, FakeMod(), typeof(TestPreference).Assembly);

    ExPreferences.LoadConfig(world.Api);
    ExPreferences.SetForPlayer("player-a", Key, "b");

    // A second store, freshly loaded off the same (real) config file backing, sees the same choice -
    // proof the value actually round-tripped through StoreModConfig/LoadModConfig rather than only
    // living in the in-memory dictionary the setter also updates.
    ExPreferences.LoadConfig(world.Api);
    Assert.Equal("b", ExPreferences.GetForPlayer("player-a", Key));
  }

  [Fact]
  public void An_unset_preference_answers_its_own_default() {
    var world = new TestWorld();
    PreferenceRegistry.RegisterAll(world.Api, FakeMod(), typeof(TestPreference).Assembly);
    ExPreferences.LoadConfig(world.Api);

    Assert.Equal("a", ExPreferences.GetForPlayer("nobody-yet", Key));
  }

  [Fact]
  public void An_unknown_key_answers_empty_rather_than_throwing() {
    var world = new TestWorld();
    ExPreferences.LoadConfig(world.Api);

    Assert.Equal(string.Empty, ExPreferences.GetForPlayer("player-a", "no-such-preference"));
  }

  [Fact]
  public void SetForPlayer_applies_the_value_to_the_registered_preference() {
    var world = new TestWorld();
    PreferenceRegistry.RegisterAll(world.Api, FakeMod(), typeof(TestPreference).Assembly);
    ExPreferences.LoadConfig(world.Api);
    var pref = (TestPreference)ExPreferences.Find(Key)!;

    ExPreferences.SetForPlayer("player-b", Key, "b");

    Assert.Equal(["b"], pref.Applied);
  }

  [Fact]
  public void ApplyForPlayer_applies_every_registered_preferences_saved_value() {
    var world = new TestWorld();
    PreferenceRegistry.RegisterAll(world.Api, FakeMod(), typeof(TestPreference).Assembly);
    ExPreferences.LoadConfig(world.Api);
    ExPreferences.SetForPlayer("player-c", Key, "b");
    var pref = (TestPreference)ExPreferences.Find(Key)!;
    pref.Applied = []; // SetForPlayer already applied it once; clear so ApplyForPlayer is what proves it

    ExPreferences.ApplyForPlayer("player-c");

    Assert.Equal(["b"], pref.Applied);
  }

  #endregion

  private static Mod FakeMod() {
    var mod = NSubstitute.Substitute.For<Mod>();
    typeof(Mod).GetProperty("Info")!.SetValue(mod, new ModInfo { ModID = "exlib", Version = "1.0.0" });
    return mod;
  }
}
