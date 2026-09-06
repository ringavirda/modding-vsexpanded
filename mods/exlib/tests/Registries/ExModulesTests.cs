using System;
using ExpandedLib.Industry;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Companion assemblies: a mod folder may hold more than one dll but only one of them may contain
/// mod systems, so the rest declare an <see cref="IExModule"/> and the mod drives them. exlib itself
/// is the worked example - <c>exlib.industry.dll</c> ships beside <c>exlib.dll</c> and is found here
/// through the <c>[assembly: ExDomain("exlib")]</c> it carries.
/// </summary>
public class ExModulesTests {
  private static Mod FakeMod(string modId, ILogger logger) {
    var mod = Substitute.For<Mod>();
    ReflectionHelpers.SetProperty(mod, nameof(Mod.Info), new ModInfo { ModID = modId });
    ReflectionHelpers.SetProperty(mod, nameof(Mod.Logger), logger);
    return mod;
  }

  [Fact]
  public void Finds_a_companion_assemblys_module_by_its_declared_domain() {
    // Touch the type first: discovery reads loaded assemblies, and the domain layer is only loaded
    // once something in this run has referenced it.
    _ = typeof(IndustryModule);
    ExModules.Reset();

    Assert.Contains(ExModules.For("exlib"), m => m is IndustryModule);
  }

  [Fact]
  public void Finds_nothing_for_a_mod_that_ships_one_assembly() {
    ExModules.Reset();

    Assert.Empty(ExModules.For("a-mod-with-no-companion-assembly"));
  }

  [Fact]
  public void A_module_that_throws_is_logged_and_the_rest_of_the_mod_continues() {
    _ = typeof(IndustryModule);
    ExModules.Reset();
    var logger = new RecordingLogger();

    // No throw escaping here is the assertion: a companion assembly is a part of the mod, and one
    // failing must not take down the phase that drives it.
    ExModules.Drive(
      FakeMod("exlib", logger),
      _ => throw new InvalidOperationException("module boom")
    );

    Assert.Contains(logger.Errors, e => e.Contains("module boom", StringComparison.Ordinal));
  }
}
