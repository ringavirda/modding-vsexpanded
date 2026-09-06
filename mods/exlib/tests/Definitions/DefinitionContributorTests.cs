using System;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="IExDefinitionContributor"/>: the asset-dependent definition hook a module or main
/// assembly entry point implements, discovered alongside a mod's classes
/// (<see cref="EntityRegistry.RegisterAll"/>) and run by <see cref="ExDefinitionModSystem.AssetsLoaded"/>
/// right before injection. <see cref="ExDefinitions"/> is a process-wide static, so the class is
/// serialized and cleared before each test; <see cref="EntityRegistry"/>'s own domain-fallback cache
/// is reset on dispose the way <c>ExModSystemTests.Dispose</c> does.
/// </summary>
[Collection("ExDefinitions")]
public class DefinitionContributorTests : IDisposable {
  public DefinitionContributorTests() {
    ExDefinitions.Clear();
    ExDefinitions.Logger = null;
  }

  public void Dispose() {
    ExDefinitions.Clear();
    ExDefinitions.Logger = null;
    var field = typeof(EntityRegistry).GetField(
      "_domainByAssembly",
      BindingFlags.NonPublic | BindingFlags.Static
    )!;
    var map = (Dictionary<Assembly, string>)field.GetValue(null)!;
    map.Remove(typeof(DefinitionContributorTests).Assembly);
  }

  private sealed class TestContributor : IExDefinitionContributor {
    public void Contribute(ICoreAPI api) =>
      ExDefinitions.RegisterItem(ExItemDef.Create("exlibtest.contrib", "contributed"));
  }

  private sealed class ThrowingContributor : IExDefinitionContributor {
    public void Contribute(ICoreAPI api) =>
      throw new InvalidOperationException("boom");
  }

#pragma warning disable CS9113 // x only needs to exist, to remove the parameterless constructor
  private sealed class NoCtorContributor(int x) : IExDefinitionContributor {
    public void Contribute(ICoreAPI api) { }
  }
#pragma warning restore CS9113

  private static Mod FakeMod(string modId) {
    var mod = Substitute.For<Mod>();
    ReflectionHelpers.SetProperty(mod, nameof(Mod.Info), new ModInfo { ModID = modId });
    return mod;
  }

  [Fact]
  public void RegisterAll_discovers_contributors_in_the_scanned_assembly() {
    var world = new TestWorld();

    EntityRegistry.RegisterAll(
      world.Api,
      FakeMod("exlibtest.contrib"),
      typeof(TestContributor).Assembly
    );

    Assert.Contains(typeof(TestContributor), ExDefinitions.Contributors);
  }

  [Fact]
  public void The_definition_system_runs_contributors_before_injecting() {
    var world = new TestWorld();
    Assert.Equal(EnumAppSide.Server, world.Api.Side);
    EntityRegistry.RegisterAll(
      world.Api,
      FakeMod("exlibtest.contrib"),
      typeof(TestContributor).Assembly
    );

    new ExDefinitionModSystem().AssetsLoaded(world.Api);

    Assert.Contains(ExDefinitions.Items, d => d.Code == "contributed");
    world
      .Api.Assets.Received()
      .Add(
        Arg.Is<AssetLocation>(l => l.Path.Contains("contributed")),
        Arg.Any<IAsset>()
      );
  }

  [Fact]
  public void A_throwing_contributor_is_logged_and_the_rest_still_run() {
    var world = new TestWorld();
    // TestContributor and ThrowingContributor share this test assembly, so one scan discovers both.
    ExDefinitions.DiscoverContributors(typeof(ThrowingContributor).Assembly);

    ExDefinitions.RunContributors(world.Api);

    Assert.Contains(world.Log.Errors, e => e.Contains("ThrowingContributor"));
    Assert.Contains(ExDefinitions.Items, d => d.Code == "contributed");
  }

  [Fact]
  public void A_contributor_without_a_parameterless_constructor_is_skipped_with_a_warning() {
    var logger = new RecordingLogger();
    ExDefinitions.Logger = logger;

    ExDefinitions.DiscoverContributors(typeof(NoCtorContributor).Assembly);

    Assert.Contains(logger.Warnings, w => w.Contains("NoCtorContributor"));
    Assert.DoesNotContain(typeof(NoCtorContributor), ExDefinitions.Contributors);
  }
}
