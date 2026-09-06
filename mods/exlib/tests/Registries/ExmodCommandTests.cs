using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Helpers;
using ExpandedLib.Migrations;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="CommandRegistry.RegisterAll"/> over the exlib assembly: which root and sub-commands
/// come out on each side, and that every one of them wires up a handler. The handler bodies
/// themselves (what typing them actually does) are driven through each command's internal dispatch
/// seam rather than through the fluent framework, the same split <see cref="RegistrySubCommandTests"/>
/// already uses for <see cref="ConfigSubCommand"/>/<see cref="RecipesSubCommand"/>.
/// </summary>
public class ExmodCommandTests {
  #region A fluent IChatCommand fake

  /// <summary>What one built command recorded: its own description, its handler (if any) and its
  /// sub-commands by name. A tree of these mirrors the tree <see cref="CommandRegistry.RegisterAll"/>
  /// builds through <c>GetOrCreate</c>/<c>BeginSubCommand</c>.</summary>
  private sealed class CommandSpy {
    public string? Description;
    public OnCommandDelegate? Handler;
    public readonly Dictionary<string, CommandSpy> SubCommands = new();
  }

  // NSubstitute auto-returns a substitute for an interface-typed return, but a fresh one on every
  // call - fine for a leaf that hangs off nowhere else, wrong for the fluent chain a real
  // IChatCommand supports, where WithDescription/BeginSubCommand/EndSubCommand must all resolve
  // back to the same logical command. Each fluent member is configured explicitly instead so the
  // whole chain (including a nested BeginSubCommand/EndSubCommand pair) threads through one spy.
  private static IChatCommand FakeCommand(CommandSpy spy, IChatCommand? parent) {
    var cmd = Substitute.For<IChatCommand>();
    cmd.WithDescription(Arg.Do<string>(d => spy.Description = d)).Returns(cmd);
    cmd.WithArgs(Arg.Any<ICommandArgumentParser[]>()).Returns(cmd);
    cmd.RequiresPrivilege(Arg.Any<string>()).Returns(cmd);
    cmd.HandleWith(Arg.Do<OnCommandDelegate>(h => spy.Handler = h)).Returns(cmd);
    cmd.BeginSubCommand(Arg.Any<string>())
      .Returns(ci => {
        string name = ci.Arg<string>();
        if (!spy.SubCommands.TryGetValue(name, out var subSpy))
          spy.SubCommands[name] = subSpy = new CommandSpy();
        return FakeCommand(subSpy, cmd);
      });
    cmd.EndSubCommand().Returns(parent ?? cmd);
    return cmd;
  }

  /// <summary>A <see cref="IChatCommandApi"/> whose <c>GetOrCreate</c> is idempotent by name - a
  /// second call for the same root returns a fresh fake wrapping the same spy, exactly as
  /// <see cref="CommandRegistry.RegisterAll"/> relies on when a sub-command resolves a parent another
  /// mod (or another pass of this loop) already created.</summary>
  private static (IChatCommandApi Api, Dictionary<string, CommandSpy> Roots) FakeChatCommandApi(
    ICoreAPI ownerApi
  ) {
    var roots = new Dictionary<string, CommandSpy>();
    var chatApi = Substitute.For<IChatCommandApi>();
    chatApi.Parsers.Returns(new CommandArgumentParsers(ownerApi));
    chatApi.GetOrCreate(Arg.Any<string>())
      .Returns(ci => {
        string name = ci.Arg<string>();
        if (!roots.TryGetValue(name, out var spy))
          roots[name] = spy = new CommandSpy();
        return FakeCommand(spy, parent: null);
      });
    return (chatApi, roots);
  }

  #endregion

  #region RegisterAll wiring

  private static Mod FakeMod() {
    var mod = Substitute.For<Mod>();
    typeof(Mod).GetProperty("Info")!.SetValue(mod, new ModInfo { ModID = "exlib", Version = "1.0.0" });
    return mod;
  }

  private static ICoreClientAPI FakeApi(
    EnumAppSide side,
    out Dictionary<string, CommandSpy> roots
  ) {
    var world = new TestWorld();
    var api = Substitute.For<ICoreClientAPI>();
    var (chatApi, r) = FakeChatCommandApi(api);
    roots = r;

    api.Side.Returns(side);
    api.ChatCommands.Returns(chatApi);
    ((ICoreAPI)api).ChatCommands.Returns(chatApi);
    api.Logger.Returns(world.Log);
    ((ICoreAPI)api).Logger.Returns(world.Log);

    var modLoader = world.Mods;
    modLoader.Register(new BlockEntityHealModSystem());
    modLoader.Register(new NetworkHighlightModSystem());
    api.ModLoader.Returns(modLoader);
    ((ICoreAPI)api).ModLoader.Returns(modLoader);

    var clientPlayer = Substitute.For<IClientPlayer>();
    clientPlayer.PlayerUID.Returns("test");
    var clientWorld = Substitute.For<IClientWorldAccessor>();
    clientWorld.Player.Returns(clientPlayer);
    api.World.Returns(clientWorld);

    return api;
  }

  [Fact]
  public void Server_side_registers_the_root_and_its_server_subcommands() {
    ICoreClientAPI api = FakeApi(EnumAppSide.Server, out var roots);

    CommandRegistry.RegisterAll(api, FakeMod(), typeof(ExmodCommand).Assembly);

    Assert.True(roots.TryGetValue("exmod", out var exmod));
    Assert.NotNull(exmod.Handler); // the root's own help handler
    foreach (string name in new[] { "config", "recipes", "verify", "heal" })
      Assert.True(
        exmod.SubCommands.TryGetValue(name, out var sub) && sub.Handler != null,
        $"expected a wired handler under exmod {name}"
      );
    // Client-only options never attach on this side.
    Assert.False(exmod.SubCommands.ContainsKey("network"));
    Assert.False(exmod.SubCommands.ContainsKey("measure"));
  }

  [Fact]
  public void Client_side_registers_the_root_and_its_client_subcommands() {
    ICoreClientAPI api = FakeApi(EnumAppSide.Client, out var roots);

    CommandRegistry.RegisterAll(api, FakeMod(), typeof(ExmodCommand).Assembly);

    Assert.True(roots.TryGetValue("exmod", out var exmod));
    Assert.NotNull(exmod.Handler);
    Assert.True(exmod.SubCommands.TryGetValue("network", out var network));
    Assert.True(network.SubCommands.TryGetValue("hi", out var hi) && hi.Handler != null);
    Assert.True(network.SubCommands.TryGetValue("unhi", out var unhi) && unhi.Handler != null);
    Assert.True(exmod.SubCommands.TryGetValue("measure", out var measure) && measure.Handler != null);
    // Server-only options never attach on this side.
    Assert.False(exmod.SubCommands.ContainsKey("config"));
    Assert.False(exmod.SubCommands.ContainsKey("recipes"));
    Assert.False(exmod.SubCommands.ContainsKey("verify"));
    Assert.False(exmod.SubCommands.ContainsKey("heal"));
  }

  #endregion

  #region VerifySubCommand.Dispatch

  // TestLang echoes every key straight back rather than formatting it (see TestLang's own doc
  // comment), so a fact can tell which lang key a branch resolved to but not the substituted
  // numbers - the message below is "exlib:command-verify-unknown", not the rendered sentence.
  [Fact]
  public void Verify_rejects_a_domain_no_loaded_mod_answers_to() {
    var world = new TestWorld();
    world.Mods.Add("stub", "1.0.0");

    TextCommandResult result = VerifySubCommand.Dispatch(world.Api, "nosuchmod");

    Assert.Equal(EnumCommandStatus.Error, result.Status);
    Assert.Contains("command-verify-unknown", result.StatusMessage);
  }

  [Fact]
  public void Verify_over_a_known_domain_with_nothing_shipped_reports_zero_errors() {
    var world = new TestWorld();
    world.Mods.Add("stub", "1.0.0");

    TextCommandResult result = VerifySubCommand.Dispatch(world.Api, "stub");

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Contains("command-verify-summary", result.StatusMessage);
  }

  #endregion

  #region HealSubCommand.Dispatch

  [Fact]
  public void Heal_reports_the_healer_s_own_count() {
    var world = new TestWorld();
    var healer = new BlockEntityHealModSystem();
    ReflectionHelpers.SetField(healer, "_sapi", world.Api);

    TextCommandResult result = HealSubCommand.Dispatch(healer);

    // No orphaned block entities in a bare world, so the healer's own count (asserted separately
    // below, since TestLang does not format the message's {0}) is zero; the command's job here is
    // only to route that count into the success result.
    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Contains("command-heal-result", result.StatusMessage);
    Assert.Equal(0, healer.HealLoadedChunks());
  }

  #endregion

  #region NetworkSubCommand.DispatchHi / DispatchUnhi

  [Fact]
  public void Network_hi_reports_success_and_does_not_throw_with_no_client_channel() {
    var highlight = new NetworkHighlightModSystem();

    TextCommandResult result = NetworkSubCommand.DispatchHi(highlight);

    Assert.Equal(EnumCommandStatus.Success, result.Status);
  }

  [Fact]
  public void Network_unhi_reports_success_and_does_not_throw_with_no_client_channel() {
    var highlight = new NetworkHighlightModSystem();

    TextCommandResult result = NetworkSubCommand.DispatchUnhi(highlight);

    Assert.Equal(EnumCommandStatus.Success, result.Status);
  }

  #endregion

  #region MeasureSubCommand.Dispatch

  [Fact]
  public void Measure_with_no_word_reports_the_current_setting() {
    var world = new TestWorld();
    ICoreClientAPI api = ClientApiFor(world, "test");
    var pref = new MeasurePreference();

    TextCommandResult result = MeasureSubCommand.Dispatch(api, "exlib", pref, rawWord: null);

    Assert.Equal(EnumCommandStatus.Success, result.Status);
  }

  [Fact]
  public void Measure_with_an_invalid_word_is_rejected() {
    var world = new TestWorld();
    ICoreClientAPI api = ClientApiFor(world, "test");
    var pref = new MeasurePreference();

    TextCommandResult result = MeasureSubCommand.Dispatch(api, "exlib", pref, "furlongs");

    Assert.Equal(EnumCommandStatus.Error, result.Status);
  }

  [Fact]
  public void Measure_with_a_valid_word_sets_the_preference_for_that_player() {
    var world = new TestWorld();
    const string uid = "measuretest";
    ICoreClientAPI api = ClientApiFor(world, uid);
    var pref = new MeasurePreference();

    TextCommandResult result = MeasureSubCommand.Dispatch(api, "exlib", pref, "imperial");

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Equal("imperial", ExPreferences.GetForPlayer(uid, pref.Key));
  }

  private static ICoreClientAPI ClientApiFor(TestWorld world, string uid) {
    var api = Substitute.For<ICoreClientAPI>();
    var player = Substitute.For<IClientPlayer>();
    player.PlayerUID.Returns(uid);
    var clientWorld = Substitute.For<IClientWorldAccessor>();
    clientWorld.Player.Returns(player);
    api.World.Returns(clientWorld);
    api.Logger.Returns(world.Log);
    return api;
  }

  #endregion
}
