using System.Linq;
using ExpandedLib.Industry;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Discovery and ordering: a module is an assembly carrying <c>[assembly: ExModule]</c>, found by
/// walking the assemblies the runtime has already loaded and kept only while its shipping mod is
/// enabled. exlib's own domain layer (<c>exlib.industry.dll</c>) is the worked example of a
/// framework module; this test assembly is a module of a fake host of its own, declared in
/// <c>ModuleInit.cs</c>.
/// </summary>
public class ExModulesTests {
  // Never given a parameterless constructor, on purpose: an entry point declared like this one is
  // reported and left out rather than crashing discovery.
  private sealed class NoCtorModule(int x) : IExModule {
    public int X => x;
  }

  [Fact]
  public void Finds_industry_as_a_framework_module() {
    // Touch the type first: discovery reads loaded assemblies, and the domain layer is only loaded
    // once something in this run has referenced it.
    _ = typeof(IndustryModule);
    var world = new TestWorld();

    ExModuleSet set = ExModules.For(world.Api, "exlib");

    ExModuleInfo module = Assert.Single(set.Modules, m => m.Id == "industry");
    Assert.Equal("exlib", module.Host);
    Assert.Contains(typeof(IndustryModule), module.EntryPoints);
    Assert.Empty(set.Errors);
  }

  [Fact]
  public void Finds_this_assembly_as_a_module_of_its_declared_host() {
    var world = new TestWorld();

    Assert.Contains(ExModules.For(world.Api, "exlibtest.host").Modules, m => m.Id == "exlibtests");
    Assert.DoesNotContain(ExModules.For(world.Api, "exlib").Modules, m => m.Id == "exlibtests");
  }

  [Fact]
  public void Finds_nothing_for_a_mod_with_no_modules() {
    var world = new TestWorld();

    ExModuleSet set = ExModules.For(world.Api, "a-mod-with-no-modules");

    Assert.Empty(set.Modules);
    Assert.Empty(set.Errors);
  }

  [Fact]
  public void IsLoaded_answers_for_any_host() {
    _ = typeof(IndustryModule);
    var world = new TestWorld();

    Assert.True(ExModules.IsLoaded(world.Api, "industry"));
    Assert.True(ExModules.IsLoaded(world.Api, "exlibtests"));
    Assert.False(ExModules.IsLoaded(world.Api, "nothing"));
  }

  [Fact]
  public void Orders_by_requires_then_by_id() {
    ExModuleInfo a = HandBuilt("a");
    ExModuleInfo b = HandBuilt("b");
    ExModuleInfo c = HandBuilt("c", requires: ["b"]);

    ExModuleSet set = ExModules.Order([c, a, b]);

    Assert.Equal(["a", "b", "c"], set.Modules.Select(m => m.Id));
    Assert.Empty(set.Errors);
  }

  [Fact]
  public void A_missing_requirement_excludes_the_module_and_names_both() {
    ExModuleInfo x = HandBuilt("x", requires: ["y"]);

    ExModuleSet set = ExModules.Order([x]);

    Assert.Empty(set.Modules);
    string error = Assert.Single(set.Errors);
    Assert.Contains("x", error);
    Assert.Contains("y", error);
  }

  [Fact]
  public void A_cycle_excludes_every_member_and_names_them() {
    ExModuleInfo a = HandBuilt("a", requires: ["b"]);
    ExModuleInfo b = HandBuilt("b", requires: ["a"]);

    ExModuleSet set = ExModules.Order([a, b]);

    Assert.Empty(set.Modules);
    string error = Assert.Single(set.Errors);
    Assert.Contains("a", error);
    Assert.Contains("b", error);
  }

  [Fact]
  public void A_duplicate_id_any_case_keeps_the_first_and_reports_the_second() {
    ExModuleInfo first = HandBuilt("dup");
    ExModuleInfo second = HandBuilt("DUP");

    ExModuleSet set = ExModules.Order([first, second]);

    Assert.Equal([first], set.Modules);
    string error = Assert.Single(set.Errors);
    Assert.Contains("dup", error);
    Assert.Contains(first.Assembly.GetName().Name!, error);
  }

  [Fact]
  public void An_entry_point_without_a_parameterless_constructor_is_reported() {
    _ = typeof(NoCtorModule);
    var world = new TestWorld();

    ExModuleSet set = ExModules.For(world.Api, "exlibtest.host");

    Assert.Contains(set.Errors, e => e.Contains("NoCtorModule"));
    ExModuleInfo module = Assert.Single(set.Modules, m => m.Id == "exlibtests");
    Assert.DoesNotContain(typeof(NoCtorModule), module.EntryPoints);
  }

  private static ExModuleInfo HandBuilt(string id, string[]? requires = null) =>
    new() {
      Id = id,
      Host = "h",
      Mod = "h",
      Requires = requires ?? [],
      Assembly = typeof(ExModulesTests).Assembly,
      EntryPoints = [],
    };
}
