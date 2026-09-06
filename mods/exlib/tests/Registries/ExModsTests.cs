using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The mod-presence rungs (<see cref="ExMods"/>) and the flag-setting ModSystem.</summary>
public class ExModsTests {
  private static Mod FakeMod(string modId, string version) {
    var mod = Substitute.For<Mod>();
    typeof(Mod)
      .GetProperty("Info")!
      .SetValue(mod, new ModInfo { ModID = modId, Version = version });
    return mod;
  }

  /// <summary>An <see cref="ICoreAPI"/> whose <c>ModLoader</c> knows exactly the given
  /// (id, version) mods and whose <c>World.Config</c> is a real, writable tree.</summary>
  private static ICoreAPI FakeApi(params (string Id, string Version)[] mods) {
    var api = Substitute.For<ICoreAPI>();
    var loader = Substitute.For<IModLoader>();
    List<Mod> loaded = mods.Select(m => FakeMod(m.Id, m.Version)).ToList();
    loader.Mods.Returns(loaded);
    for (int i = 0; i < mods.Length; i++)
      loader.GetMod(mods[i].Id).Returns(loaded[i]);
    loader
      .IsModEnabled(Arg.Any<string>())
      .Returns(call => loaded.Any(m => m.Info.ModID == (string)call[0]));
    api.ModLoader.Returns(loader);

    var world = Substitute.For<IWorldAccessor>();
    world.Config.Returns(new TreeAttribute());
    api.World.Returns(world);

    return api;
  }

  #region IsLoaded
  [Fact]
  public void IsLoaded_true_for_an_enabled_mod() {
    var api = FakeApi(("toolsmith", "1.0.0"));
    Assert.True(ExMods.IsLoaded(api, "toolsmith"));
  }

  [Fact]
  public void IsLoaded_false_for_a_mod_not_present() {
    var api = FakeApi(("toolsmith", "1.0.0"));
    Assert.False(ExMods.IsLoaded(api, "other"));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  public void IsLoaded_false_and_never_throws_for_a_blank_id(string? modId) {
    var api = FakeApi(("toolsmith", "1.0.0"));
    Assert.False(ExMods.IsLoaded(api, modId!));
  }
  #endregion

  #region Version
  [Fact]
  public void Version_returns_the_loaded_mods_version() {
    var api = FakeApi(("toolsmith", "1.9.0-rc.1"));
    Assert.Equal("1.9.0-rc.1", ExMods.Version(api, "toolsmith"));
  }

  [Fact]
  public void Version_is_null_when_absent() {
    var api = FakeApi();
    Assert.Null(ExMods.Version(api, "toolsmith"));
  }
  #endregion

  #region AtLeast
  [Fact]
  public void AtLeast_true_when_the_loaded_version_meets_the_floor() {
    var api = FakeApi(("toolsmith", "1.9.0"));
    Assert.True(ExMods.AtLeast(api, "toolsmith", "1.9.0"));
  }

  [Fact]
  public void AtLeast_true_when_the_loaded_version_exceeds_the_floor() {
    var api = FakeApi(("toolsmith", "1.10.0"));
    Assert.True(ExMods.AtLeast(api, "toolsmith", "1.9.0"));
  }

  [Fact]
  public void AtLeast_false_when_a_prerelease_is_below_its_own_release() {
    // A release candidate sorts below the stable release it precedes, same as GameVersion itself.
    var api = FakeApi(("toolsmith", "1.9.0-rc.1"));
    Assert.False(ExMods.AtLeast(api, "toolsmith", "1.9.0"));
  }

  [Fact]
  public void AtLeast_false_when_the_mod_is_absent() {
    var api = FakeApi();
    Assert.False(ExMods.AtLeast(api, "toolsmith", "1.9.0"));
  }

  [Fact]
  public void AtLeast_false_when_the_loaded_version_is_older() {
    var api = FakeApi(("toolsmith", "1.8.0"));
    Assert.False(ExMods.AtLeast(api, "toolsmith", "1.9.0"));
  }
  #endregion

  #region WhenLoaded
  [Fact]
  public void WhenLoaded_runs_the_action_and_returns_true_when_present() {
    var api = FakeApi(("toolsmith", "1.0.0"));
    int ran = 0;

    bool result = ExMods.WhenLoaded(api, "toolsmith", () => ran++);

    Assert.True(result);
    Assert.Equal(1, ran);
  }

  [Fact]
  public void WhenLoaded_never_runs_and_returns_false_when_absent() {
    var api = FakeApi();
    int ran = 0;

    bool result = ExMods.WhenLoaded(api, "toolsmith", () => ran++);

    Assert.False(result);
    Assert.Equal(0, ran);
  }
  #endregion

  [Fact]
  public void FlagKey_is_the_exlib_mod_prefixed_key() {
    Assert.Equal("exlib:mod:toolsmith", ExMods.FlagKey("toolsmith"));
  }

  #region ExModsModSystem
  [Fact]
  public void StartPre_sets_one_flag_per_enabled_mod() {
    var api = FakeApi(("toolsmith", "1.0.0"), ("other", "2.0.0"));
    var system = new ExModsModSystem();

    system.StartPre(api);

    Assert.True(api.World.Config.GetBool(ExMods.FlagKey("toolsmith")));
    Assert.True(api.World.Config.GetBool(ExMods.FlagKey("other")));
  }

  [Fact]
  public void Start_also_sets_the_flags() {
    // Belt and braces: if World.Config is not yet populated at StartPre on some side, Start still
    // sets it, well before the JSON patch loader's AssetsLoaded at 0.05.
    var api = FakeApi(("toolsmith", "1.0.0"));
    var system = new ExModsModSystem();

    system.Start(api);

    Assert.True(api.World.Config.GetBool(ExMods.FlagKey("toolsmith")));
  }
  #endregion
}
