using System.Linq;
using ExpandedLib.Config;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The host-to-client leg of a config's live values: <see cref="IExConfigAccess.ExportJson"/>/
/// <see cref="IExConfigAccess.ImportJson"/> on the store, the wire packet, and
/// <see cref="ExConfigSyncModSystem"/>'s join push and unknown-section handling. Reuses
/// <c>FakeConfig</c> from <c>ConfigMigrationTests</c>.
/// </summary>
public class ConfigSyncTests {
  #region ExportJson / ImportJson
  [Fact]
  public void ExportJson_round_trips_through_ImportJson() {
    var source = new ExConfigRegister<FakeConfig>("fake.json", "fakemod");
    source.Config.ValueA = 5;
    source.Config.ValueB = 9;
    source.Config.Label = "custom";

    var target = new ExConfigRegister<FakeConfig>("fake.json", "fakemod");
    target.ImportJson(source.ExportJson());

    Assert.Equal(5, target.Config.ValueA);
    Assert.Equal(9, target.Config.ValueB);
    Assert.Equal("custom", target.Config.Label);
  }

  [Fact]
  public void ImportJson_clamps_an_out_of_range_value_exactly_as_Load_does() {
    var store = new ExConfigRegister<FakeConfig>("fake.json", "fakemod");

    // ValueA has no [ExConfigRange], so it defaults to the non-negative floor Load enforces.
    store.ImportJson("{\"ValueA\":-5,\"ValueB\":7}");

    Assert.Equal(100, store.Config.ValueA); // reset to the coded default, same as a bad load
    Assert.Equal(7, store.Config.ValueB); // in-range value kept
  }

  [Fact]
  public void ImportJson_never_persists_to_disk() {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    api.Side.Returns(EnumAppSide.Server);

    var mod = Substitute.For<Mod>();
    typeof(Mod)
      .GetProperty("Info")!
      .SetValue(mod, new ModInfo { Version = "1.0.0" });
    var modLoader = Substitute.For<IModLoader>();
    modLoader.GetMod("fakemod").Returns(mod);
    api.ModLoader.Returns(modLoader);
    api.LoadModConfig<JObject>("fake.json").Returns((JObject?)null);

    int saveCount = 0;
    api.When(a => a.StoreModConfig(Arg.Any<JObject>(), "fake.json"))
      .Do(_ => saveCount++);

    var store = new ExConfigRegister<FakeConfig>("fake.json", "fakemod");
    store.Load(api); // server side: Load writes the file back once
    Assert.Equal(1, saveCount);

    store.ImportJson("{\"ValueA\":7}");

    Assert.Equal(1, saveCount); // unchanged - importing a synced section must not write the file
    Assert.Equal(7, store.Config.ValueA);
  }
  #endregion

  #region ConfigSyncPacket
  [Fact]
  public void ConfigSyncPacket_round_trips_through_the_game_serializer() {
    var packet = new ConfigSyncPacket {
      ModId = "fakemod",
      FileName = "fake.json",
      Json = "{\"ValueA\":5}",
    };

    byte[] bytes = SerializerUtil.Serialize(packet);
    var result = SerializerUtil.Deserialize<ConfigSyncPacket>(bytes);

    Assert.Equal("fakemod", result.ModId);
    Assert.Equal("fake.json", result.FileName);
    Assert.Equal("{\"ValueA\":5}", result.Json);
  }
  #endregion

  #region ExConfigSyncModSystem
  [Fact]
  public void HandlePacket_imports_into_the_matching_registered_section() {
    var store = new ExConfigRegister<FakeConfig>("fake.json", "synctest.known");
    ExConfigProfiles.Register(store);

    var api = Substitute.For<ICoreClientAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());

    ExConfigSyncModSystem.HandlePacket(
      api,
      new ConfigSyncPacket {
        ModId = "synctest.known",
        FileName = "fake.json",
        Json = "{\"ValueA\":42}",
      }
    );

    Assert.Equal(42, store.Config.ValueA);
    api.Logger.Received(1).Notification(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void HandlePacket_warns_and_drops_an_unknown_section() {
    var api = Substitute.For<ICoreClientAPI>();
    var logger = Substitute.For<ILogger>();
    api.Logger.Returns(logger);

    ExConfigSyncModSystem.HandlePacket(
      api,
      new ConfigSyncPacket {
        ModId = "synctest.unregistered",
        FileName = "fake.json",
        Json = "{}",
      }
    );

    logger.Received(1).Warning(Arg.Any<string>(), Arg.Any<object[]>());
    logger.DidNotReceive().Notification(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void PlayerJoin_sends_one_packet_per_registered_section() {
    var storeA = new ExConfigRegister<FakeConfig>("fake.json", "synctest.join.a");
    var storeB = new ExConfigRegister<FakeConfig>("fake.json", "synctest.join.b");
    ExConfigProfiles.Register(storeA);
    ExConfigProfiles.Register(storeB);

    using var world = new TestWorld();
    var system = new ExConfigSyncModSystem();
    system.StartServerSide(world.Api);

    // "exlibConfigSync": the channel name ExConfigSyncModSystem registers under.
    var channels = world.Channels("exlibConfigSync");
    world.Api.Event.PlayerJoin += Raise.Event<PlayerDelegate>(channels.Sender);

    // ExConfigProfiles is process-wide, so other test classes' sections may also be in here - assert
    // exactly one packet per section, not on the total, exactly as the substituted-channel version
    // did with Received(1).
    Assert.Single(
      channels.SentToClients,
      p => ((ConfigSyncPacket)p).ModId == "synctest.join.a"
    );
    Assert.Single(
      channels.SentToClients,
      p => ((ConfigSyncPacket)p).ModId == "synctest.join.b"
    );
  }
  #endregion
}
