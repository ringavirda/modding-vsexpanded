using ExpandedLib.Registries.Config;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>A throwaway versioned config POCO; property initialisers define the coded defaults.</summary>
internal sealed class FakeConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }
  public int ValueA { get; set; } = 100;
  public int ValueB { get; set; } = 200;
  public float Rate { get; set; } = 1.5f;
  public string Label { get; set; } = "ok";
}

/// <summary>
/// The shared config store's load-migrate-stamp-save cycle: a value reset fires exactly when an
/// upgrade crosses its <c>ToVersion</c>, scoped optionally by <c>FromVersion</c>, and only the named
/// fields are reset while everything else (and the version stamp) round-trips.
/// </summary>
public class ConfigMigrationTests {
  private const string ModId = "fakemod";
  private const string FileName = "fake.json";

  /// <summary>A fake mod stamped with <paramref name="version"/>. <c>Mod.Info</c> has a non-public
  /// setter, so it is assigned through reflection.</summary>
  private static Mod FakeMod(string version) {
    var mod = Substitute.For<Mod>();
    typeof(Mod)
      .GetProperty("Info")!
      .SetValue(mod, new ModInfo { Version = version });
    return mod;
  }

  /// <summary>Builds a fake server API whose <c>LoadModConfig</c> returns <paramref name="stored"/>
  /// and whose mod version is <paramref name="runningVersion"/>. Captures whatever is saved back.</summary>
  private static (ICoreAPI api, System.Func<FakeConfig?> saved) FakeApi(
    FakeConfig? stored,
    string runningVersion,
    EnumAppSide side = EnumAppSide.Server
  ) {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    // Only the server writes the file back, so an unstubbed Side (default 0, neither side) would
    // silently turn every write-back assertion below into a check on a no-op.
    api.Side.Returns(side);

    // The store reads and writes its "fakemod" section of a shared mod-sectioned document.
    JObject? doc =
      stored == null
        ? null
        : new JObject { [ModId] = JObject.FromObject(stored) };
    api.LoadModConfig<JObject>(FileName).Returns(doc);

    var modLoader = Substitute.For<IModLoader>();
    modLoader.GetMod(ModId).Returns(FakeMod(runningVersion));
    api.ModLoader.Returns(modLoader);

    JObject? captured = null;
    api.When(a => a.StoreModConfig(Arg.Any<JObject>(), FileName))
      .Do(ci => captured = ci.Arg<JObject>());

    return (api, () => captured?[ModId]?.ToObject<FakeConfig>());
  }

  private static ExConfigRegister<FakeConfig> Store(
    params ExConfigMigration[] m
  ) => new(FileName, ModId, m);

  [Fact]
  public void Load_resets_invalid_values_to_defaults_but_keeps_valid_ones() {
    // A player has hand-edited the file into gameplay-breaking values.
    var stored = new FakeConfig {
      ConfigVersion = "1.0.0",
      ValueA = -3, // negative int
      ValueB = 7, // valid - keep
      Rate = float.NaN, // not-a-number
      Label = null!, // missing/null string
    };
    var (api, saved) = FakeApi(stored, runningVersion: "1.0.0");
    var store = Store();

    store.Load(api);

    Assert.Equal(100, store.Config.ValueA); // reset to default
    Assert.Equal(7, store.Config.ValueB); // valid value kept
    Assert.Equal(1.5f, store.Config.Rate, 3); // reset to default
    Assert.Equal("ok", store.Config.Label); // reset to default
    Assert.Equal(100, saved()!.ValueA); // repaired config written back to disk
  }

  [Fact]
  public void Missing_file_loads_coded_defaults_and_stamps_version() {
    var (api, saved) = FakeApi(stored: null, runningVersion: "1.0.0");
    var store = Store();

    store.Load(api);

    Assert.Equal(100, store.Config.ValueA);
    Assert.Equal(200, store.Config.ValueB);
    Assert.Equal("1.0.0", store.Config.ConfigVersion);
    Assert.Equal("1.0.0", saved()!.ConfigVersion); // written back to disk
  }

  [Fact]
  public void Same_version_preserves_all_player_tuning() {
    var stored = new FakeConfig {
      ConfigVersion = "1.0.0",
      ValueA = 5,
      ValueB = 7,
    };
    var (api, _) = FakeApi(stored, runningVersion: "1.0.0");
    var store = Store(
      new ExConfigMigration { ToVersion = "1.0.0", ResetFields = ["ValueA"] }
    );

    store.Load(api);

    // The build is unchanged, so no migration runs even though one targets this version.
    Assert.Equal(5, store.Config.ValueA);
    Assert.Equal(7, store.Config.ValueB);
  }

  [Fact]
  public void Crossing_migration_resets_only_the_named_field() {
    var stored = new FakeConfig {
      ConfigVersion = "0.9.0",
      ValueA = 5,
      ValueB = 7,
    };
    var (api, _) = FakeApi(stored, runningVersion: "0.9.2");
    var store = Store(
      new ExConfigMigration { ToVersion = "0.9.1", ResetFields = ["ValueA"] }
    );

    store.Load(api);

    Assert.Equal(100, store.Config.ValueA); // reset to default
    Assert.Equal(7, store.Config.ValueB); // untouched
    Assert.Equal("0.9.2", store.Config.ConfigVersion);
  }

  [Fact]
  public void Empty_reset_fields_resets_the_whole_config() {
    var stored = new FakeConfig {
      ConfigVersion = "0.9.0",
      ValueA = 5,
      ValueB = 7,
    };
    var (api, _) = FakeApi(stored, runningVersion: "1.0.0");
    var store = Store(new ExConfigMigration { ToVersion = "1.0.0" });

    store.Load(api);

    Assert.Equal(100, store.Config.ValueA);
    Assert.Equal(200, store.Config.ValueB);
  }

  [Fact]
  public void Migration_above_the_running_build_does_not_fire() {
    var stored = new FakeConfig { ConfigVersion = "0.9.0", ValueA = 5 };
    var (api, _) = FakeApi(stored, runningVersion: "0.9.1");
    var store = Store(
      // Resets only for builds at or above 0.9.5; the running build is 0.9.1.
      new ExConfigMigration { ToVersion = "0.9.5", ResetFields = ["ValueA"] }
    );

    store.Load(api);

    Assert.Equal(5, store.Config.ValueA);
  }

  [Fact]
  public void FromVersion_lower_bound_scopes_the_reset() {
    // The file was saved at 0.9.0, below the migration's FromVersion of 0.9.1, so the scoped
    // "0.9.1 => 0.9.2" reset must not touch it.
    var stored = new FakeConfig { ConfigVersion = "0.9.0", ValueA = 5 };
    var (api, _) = FakeApi(stored, runningVersion: "0.9.2");
    var store = Store(
      new ExConfigMigration {
        ToVersion = "0.9.2",
        FromVersion = "0.9.1",
        ResetFields = ["ValueA"],
      }
    );

    store.Load(api);

    Assert.Equal(5, store.Config.ValueA);
  }

  [Fact]
  public void Unparseable_stored_version_is_treated_as_oldest_and_migrates() {
    // Pre-versioning file (null stamp) sorts lowest, so an unscoped migration fires.
    var stored = new FakeConfig { ConfigVersion = null, ValueA = 5 };
    var (api, _) = FakeApi(stored, runningVersion: "0.9.2");
    var store = Store(
      new ExConfigMigration { ToVersion = "0.9.1", ResetFields = ["ValueA"] }
    );

    store.Load(api);

    Assert.Equal(100, store.Config.ValueA);
  }

  [Fact]
  public void Load_failure_falls_back_to_defaults_without_throwing() {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.LoadModConfig<JObject>(FileName)
      .Returns(_ => throw new System.Exception("corrupt json"));
    var modLoader = Substitute.For<IModLoader>();
    modLoader.GetMod(ModId).Returns(FakeMod("1.0.0"));
    api.ModLoader.Returns(modLoader);

    var store = Store();
    store.Load(api); // must not throw

    Assert.Equal(100, store.Config.ValueA);
    Assert.Equal("1.0.0", store.Config.ConfigVersion);
  }

  [Fact]
  public void A_client_side_load_reads_the_config_but_never_writes_it() {
    // In singleplayer both sides load this register in the same process against the same file. Two
    // writers race over it, so only the server writes.
    var stored = new FakeConfig { ConfigVersion = "1.0.0", ValueA = 42 };
    var (api, saved) = FakeApi(
      stored,
      runningVersion: "1.0.0",
      side: EnumAppSide.Client
    );
    var store = Store();

    store.Load(api);

    Assert.Equal(42, store.Config.ValueA);
    Assert.Null(saved());
  }
}
