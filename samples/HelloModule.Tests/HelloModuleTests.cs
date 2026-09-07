using System.Linq;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace HelloModule.Tests;

/// <summary>
/// The whole "ship it as a module" walk: exlib discovers this assembly as a framework module through
/// its <c>[assembly: ExModule]</c>, the pure item-family builder emits one item per greeting, and its
/// block behaviour registers under its own domain rather than a caller's.
/// </summary>
public class HelloModuleTests {
  private static Mod FakeMod(string modId) {
    var mod = Substitute.For<Mod>();
    ReflectionHelpers.SetProperty(
      mod,
      nameof(Mod.Info),
      new ModInfo { ModID = modId }
    );
    ReflectionHelpers.SetProperty(
      mod,
      nameof(Mod.Logger),
      new RecordingLogger()
    );
    return mod;
  }

  [Fact]
  public void Is_discovered_as_a_framework_module() {
    var world = new TestWorld();

    ExModuleSet set = ExModules.For(world.Api, "exlib");

    ExModuleInfo? info = set.Modules.FirstOrDefault(m => m.Id == "hellomodule");

    Assert.NotNull(info);
    Assert.Contains(typeof(HelloModule), info!.EntryPoints);
  }

  [Fact]
  public void Emits_one_item_per_greeting() {
    var greetings = new[]
    {
      new GreetingDef { Code = "a", Text = "Hi" },
      new GreetingDef { Code = "b", Text = "Yo" },
    };

    string[] codes = GreetingItems
      .Emit("hellomodule", greetings)
      .Select(def => def.Code)
      .ToArray();

    Assert.Equal(["greeting-a", "greeting-b"], codes);
  }

  [Fact]
  public void Registers_its_behaviour_under_its_own_domain() {
    var world = new TestWorld();
    var host = new ExModuleHost(FakeMod("exlib"), world.Api);

    host.Start(world.Api);

    world
      .Api.Received()
      .RegisterBlockBehaviorClass(
        "hellomodule.BlockBehaviorGreeter",
        typeof(BlockBehaviorGreeter)
      );
  }

  [Fact]
  public void Resolves_its_behaviour_key_across_assemblies() {
    // BlockHello's def names this behaviour through Class<BlockBehaviorGreeter>() from
    // HelloExpanded's own domain; it must still resolve to hellomodule's, not the caller's.
    Assert.Equal(
      "hellomodule.BlockBehaviorGreeter",
      EntityRegistry.KeyFor("helloexpanded", typeof(BlockBehaviorGreeter))
    );
  }
}
