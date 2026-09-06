using System;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using HarmonyLib;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The zero-line registration rung. Each lifecycle hook runs the matching registries for that phase
/// before the mod's own (empty by default) hook: config and entities in <c>Start</c>, commands in
/// each side hook, preferences before commands on the client, and Harmony patched only when
/// <c>PatchHarmony</c> opts in. Joins <see cref="ExHarmonyCollection"/> - the Harmony cases patch a
/// real, process-wide target.
/// </summary>
[Collection(ExHarmonyCollection.Name)]
public class ExModSystemTests : IDisposable {
  // EntityRegistry.RegisterAll records this assembly against whichever mod id last registered it
  // (EntityRegistry's own domain-fallback cache), so every test here would otherwise leave this
  // shared test assembly pointing at a throwaway test mod id for the rest of the run - breaking
  // any other test's EntityRegistry.KeyFor/DomainOf call against it. Reset on dispose, the same way
  // ExHarmonyTests resets the Harmony patches it applies.
  public void Dispose() {
    var field = typeof(EntityRegistry).GetField(
      "_domainByAssembly",
      BindingFlags.NonPublic | BindingFlags.Static
    )!;
    var map = (Dictionary<Assembly, string>)field.GetValue(null)!;
    map.Remove(typeof(ExModSystemTests).Assembly);
  }
  [BlockRegister]
  private sealed class TestBlock : Block { }

  [SubCommandRegister(Side = EnumAppSide.Server)]
  private sealed class TestServerSubCommand : IExSubCommand {
    public string ParentName => "exmod";

    public void Register(ICoreAPI api, Mod mod, IChatCommand parent) =>
      parent
        .BeginSubCommand("exmodsystemtest-server")
        .HandleWith(_ => TextCommandResult.Success())
        .EndSubCommand();
  }

  [SubCommandRegister(Side = EnumAppSide.Client)]
  private sealed class TestClientSubCommand : IExSubCommand {
    public string ParentName => "exmod";

    public void Register(ICoreAPI api, Mod mod, IChatCommand parent) =>
      parent
        .BeginSubCommand("exmodsystemtest-client")
        .HandleWith(_ => TextCommandResult.Success())
        .EndSubCommand();
  }

  [PreferenceRegister]
  private sealed class TestSystemPreference : IExPreference {
    public string Key => "exmodsystemtestpref";
    public IReadOnlyList<string> Options { get; } = ["a", "b"];
    public string Default => "a";
    public void Apply(string value) { }
  }

  // A private target of this test class only, so its own patch count is never touched by another
  // test file's uncategorised [HarmonyPatch] classes also living in this assembly (PatchOnce scans
  // the whole assembly, so every such class gets applied under whichever mod id is patching - each
  // targets its own private type, so their counts never mix).
  private static class HarmonyTarget {
    public static void Method() { }
  }

  [HarmonyPatch(typeof(HarmonyTarget), nameof(HarmonyTarget.Method))]
  private static class HarmonyTargetPatch {
    private static void Prefix() { }
  }

  // A module of "exlibtest.host" through the test assembly's own [assembly: ExModule] (see
  // ModuleInit.cs); Hosting_a_module_runs_it_through_the_mods_phases proves ExModSystem drives it.
  private sealed class RecordingModule : IExModule {
    public static readonly List<string> Phases = [];

    public void Start(ICoreAPI api) => Phases.Add("Start");

    public void Dispose() => Phases.Add("Dispose");
  }

  private sealed class RecordingModSystem(bool patchHarmony) : ExModSystem {
    public List<string> Order { get; } = [];
    protected override bool PatchHarmony => patchHarmony;

    protected override void OnStart(ICoreAPI api) => Order.Add("OnStart");

    protected override void OnStartServerSide(ICoreServerAPI api) =>
      Order.Add("OnStartServerSide");

    protected override void OnStartClientSide(ICoreClientAPI api) =>
      Order.Add("OnStartClientSide");

    protected override void OnAssetsFinalize(ICoreAPI api) =>
      Order.Add("OnAssetsFinalize");
  }

  private static RecordingModSystem NewSystem(Mod mod, bool patchHarmony = false) {
    var system = new RecordingModSystem(patchHarmony);
    ReflectionHelpers.SetProperty(system, nameof(ModSystem.Mod), mod);
    return system;
  }

  private static Mod FakeMod(string modId) {
    var mod = Substitute.For<Mod>();
    ReflectionHelpers.SetProperty(mod, nameof(Mod.Info), new ModInfo { ModID = modId });
    // The module host logs through this when an entry point of a hosted module throws (see
    // Hosting_a_module_runs_it_through_the_mods_phases, which shares this test assembly's
    // "exlibtests" module with ExModuleHostTests' own throwing case).
    ReflectionHelpers.SetProperty(mod, nameof(Mod.Logger), new RecordingLogger());
    return mod;
  }

  #region Start

  [Fact]
  public void Start_registers_entities_loads_config_and_then_runs_OnStart() {
    var world = new TestWorld();
    var system = NewSystem(FakeMod("exlibtest.exmodsystem-start"));

    system.Start(world.Api);

    world
      .Api.Received(1)
      .RegisterBlockClass(
        Arg.Is<string>(k => k.EndsWith("TestBlock")),
        typeof(TestBlock)
      );
    Assert.Equal(42, ExModSystemTestValues.Tunable);
    Assert.Equal(["OnStart"], system.Order);
  }

  [Fact]
  public void Hosting_a_module_runs_it_through_the_mods_phases() {
    RecordingModule.Phases.Clear();
    var world = new TestWorld();
    var system = NewSystem(FakeMod("exlibtest.host"));

    system.Start(world.Api);
    system.Dispose();

    Assert.Contains("Start", RecordingModule.Phases);
    Assert.Contains("Dispose", RecordingModule.Phases);
  }

  #endregion

  #region StartServerSide / StartClientSide

  [Fact]
  public void StartServerSide_registers_server_commands_then_runs_the_hook() {
    var world = new TestWorld();
    var system = NewSystem(FakeMod("exlibtest.exmodsystem-server"));

    system.StartServerSide(world.Api);

    Assert.Equal(["OnStartServerSide"], system.Order);
  }

  [Fact]
  public void StartClientSide_registers_the_preference_before_running_the_hook() {
    var world = new TestWorld();
    var system = NewSystem(FakeMod("exlibtest.exmodsystem-client"));

    system.StartClientSide(world.ClientApi);

    Assert.IsType<TestSystemPreference>(ExPreferences.Find("exmodsystemtestpref"));
    Assert.Equal(["OnStartClientSide"], system.Order);
  }

  #endregion

  #region AssetsFinalize

  [Fact]
  public void AssetsFinalize_runs_the_hook() {
    var world = new TestWorld();
    var system = NewSystem(FakeMod("exlibtest.exmodsystem-finalize"));

    system.AssetsFinalize(world.Api);

    Assert.Equal(["OnAssetsFinalize"], system.Order);
  }

  #endregion

  #region PatchHarmony

  [Fact]
  public void PatchHarmony_true_patches_on_Start_and_unpatches_on_Dispose() {
    var mod = FakeMod("exlibtest.exmodsystem-harmony");
    var system = NewSystem(mod, patchHarmony: true);
    var world = new TestWorld();
    MethodBase original = typeof(HarmonyTarget).GetMethod(
      nameof(HarmonyTarget.Method)
    )!;

    try {
      system.Start(world.Api);
      Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);

      system.Dispose();
      var info = Harmony.GetPatchInfo(original);
      Assert.True(info == null || info.Prefixes.Count == 0);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void PatchHarmony_false_never_patches() {
    var mod = FakeMod("exlibtest.exmodsystem-no-harmony");
    var system = NewSystem(mod, patchHarmony: false);
    var world = new TestWorld();
    MethodBase original = typeof(HarmonyTarget).GetMethod(
      nameof(HarmonyTarget.Method)
    )!;

    system.Start(world.Api);
    system.Dispose();

    var info = Harmony.GetPatchInfo(original);
    Assert.True(info == null || info.Prefixes.Count == 0);
  }

  #endregion
}
