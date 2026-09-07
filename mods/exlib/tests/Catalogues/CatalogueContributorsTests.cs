using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Registries;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shared contributor contract every catalogue registry exposes, and its use by all six catalogues:
/// a C# entry survives the clear that precedes each <c>AssetsFinalize</c> read because the loader
/// re-invokes it every time. Shares the "MetalRegistry" collection with the other classes that mutate
/// that process-wide static, since one of the cases below does too.
/// </summary>
[Collection("MetalRegistry")]
public class CatalogueContributorsTests {
  #region CatalogueContributors itself
  [Fact]
  public void A_registered_contributor_runs_with_the_given_api() {
    var contributors = new CatalogueContributors();
    var api = Substitute.For<ICoreAPI>();
    ICoreAPI? seen = null;
    contributors.Register(a => seen = a);

    contributors.Invoke(api, Substitute.For<ILogger>());

    Assert.Same(api, seen);
    Assert.Equal(1, contributors.Count);
  }

  [Fact]
  public void Clear_drops_every_contributor() {
    var contributors = new CatalogueContributors();
    int ran = 0;
    contributors.Register(_ => ran++);
    contributors.Clear();

    contributors.Invoke(Substitute.For<ICoreAPI>(), Substitute.For<ILogger>());

    Assert.Equal(0, ran);
    Assert.Equal(0, contributors.Count);
  }

  [Fact]
  public void A_throwing_contributor_is_logged_and_does_not_stop_the_others() {
    var contributors = new CatalogueContributors();
    var logger = Substitute.For<ILogger>();
    int ran = 0;
    contributors.Register(_ =>
      throw new System.InvalidOperationException("boom")
    );
    contributors.Register(_ => ran++);

    contributors.Invoke(Substitute.For<ICoreAPI>(), logger);

    Assert.Equal(1, ran);
    logger.Received(1).Error(Arg.Any<string>(), Arg.Any<object[]>());
  }
  #endregion

  #region Every catalogue's Contributors survives a reload
  [Fact]
  public void A_metal_registered_from_C_sharp_survives_two_loads() {
    MetalRegistry.Clear();
    MetalRegistry.Contributors.Clear();
    MetalRegistry.Contributors.Register(_ =>
      MetalRegistry.Register(
        new MetalDef { Code = "cshargot", MoltenItem = "test:ingot-cshargot" }
      )
    );

    MetalCatalogueLoader.Populate([], [], null, null);
    MetalRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.True(MetalRegistry.TryGet("test:ingot-cshargot", out _));

    MetalRegistry.Clear();
    MetalCatalogueLoader.Populate([], [], null, null);
    MetalRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.True(MetalRegistry.TryGet("test:ingot-cshargot", out _));

    MetalRegistry.Contributors.Clear();
  }

  [Fact]
  public void A_liquid_registered_from_C_sharp_survives_two_loads() {
    ExLiquids.Clear();
    ExLiquids.Contributors.Clear();
    ExLiquids.Contributors.Register(_ =>
      ExLiquids.Register(new LiquidDef { Code = "Brine" })
    );

    ExLiquids.SeedDefaults();
    ExLiquids.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.True(ExLiquids.TryGet("Brine", out _));

    ExLiquids.Clear();
    ExLiquids.SeedDefaults();
    ExLiquids.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.True(ExLiquids.TryGet("Brine", out _));

    ExLiquids.Contributors.Clear();
  }

  [Fact]
  public void A_material_role_registered_from_C_sharp_survives_two_loads() {
    MaterialRoleRegistry.Clear();
    MaterialRoleRegistry.Contributors.Clear();
    MaterialRoleRegistry.Contributors.Register(_ =>
      MaterialRoleRegistry.Register(
        new MaterialRoleDef { Role = "fuel", Code = "test:coal" }
      )
    );

    MaterialRoleRegistry.InvokeContributors(Substitute.For<ICoreAPI>());
    Assert.True(
      MaterialRoleRegistry.IsRole("fuel", new AssetLocation("test:coal"))
    );

    MaterialRoleRegistry.Clear();
    MaterialRoleRegistry.InvokeContributors(Substitute.For<ICoreAPI>());
    Assert.True(
      MaterialRoleRegistry.IsRole("fuel", new AssetLocation("test:coal"))
    );

    MaterialRoleRegistry.Contributors.Clear();
  }

  [Fact]
  public void A_route_registered_from_C_sharp_survives_two_loads() {
    var registry = new ProcessRouteRegistry();
    var ex = new ProcessExtensions(registry, new ProcessJobRegistry());
    ProcessRouteRegistry.Contributors.Clear();
    ProcessRouteRegistry.Contributors.Register(_ =>
      ex.AddStages("cshargot", [new ProcessStage(2.0f, "Bar", ["flat"], null)])
    );

    registry.Clear();
    ProcessRouteRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.NotNull(registry.Route("cshargot"));

    registry.Clear();
    ProcessRouteRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.NotNull(registry.Route("cshargot"));

    ProcessRouteRegistry.Contributors.Clear();
  }

  [Fact]
  public void A_job_registered_from_C_sharp_survives_two_loads() {
    var registry = new ProcessJobRegistry();
    var ex = new ProcessExtensions(new ProcessRouteRegistry(), registry);
    ProcessJobRegistry.Contributors.Clear();
    ProcessJobRegistry.Contributors.Register(_ =>
      ex.AddJobs(
        "cshargotpress",
        [new ProcessJob("test:strip", "test:rivet", 6, null, null, 0f)]
      )
    );

    registry.Clear();
    ProcessJobRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.NotNull(registry.Job("cshargotpress", "test:strip", null, null));

    registry.Clear();
    ProcessJobRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.NotNull(registry.Job("cshargotpress", "test:strip", null, null));

    ProcessJobRegistry.Contributors.Clear();
  }

  [Fact]
  public void A_bay_rule_registered_from_C_sharp_survives_two_loads() {
    var registry = new BayOccupancyRegistry();
    BayOccupancyRegistry.Contributors.Clear();
    BayOccupancyRegistry.Contributors.Register(_ =>
      registry.Contribute(
        new BayOccupancySet(
          "cshargotrack",
          [new BayOccupancy("test:stock-rod", 1)]
        )
      )
    );

    registry.Clear();
    BayOccupancyRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.Equal(1, registry.CellsFor("cshargotrack", "test:stock-rod"));

    registry.Clear();
    BayOccupancyRegistry.Contributors.Invoke(
      Substitute.For<ICoreAPI>(),
      Substitute.For<ILogger>()
    );
    Assert.Equal(1, registry.CellsFor("cshargotrack", "test:stock-rod"));

    BayOccupancyRegistry.Contributors.Clear();
  }
  #endregion
}
