using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Materials;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The overlay that fills <see cref="MaterialRoleRegistry"/> at <c>AssetsFinalize</c>, and the
/// <see cref="MaterialRoleDef"/> / <see cref="MaterialRoleCatalogue"/> JSON binding a content mod ships.
/// The asset read itself needs a running game (verified in-game); the load-bearing logic - valid-def
/// registration, malformed-def skipping, and the camelCase→POCO mapping - is pinned here against the
/// asset-free <see cref="MaterialRoleLoader.Overlay"/>.
/// </summary>
[Collection("MaterialRoles")] // shares the process-wide registry with MaterialRoleRegistryTests
public class MaterialRoleLoaderTests
{
  public MaterialRoleLoaderTests() => MaterialRoleRegistry.Clear();

  #region Overlay
  [Fact]
  public void Overlay_registers_every_valid_def()
  {
    MaterialRoleLoader.Overlay(
      new[]
      {
        new MaterialRoleCatalogue
        {
          Materials = new()
          {
            new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" },
            new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" },
          },
        },
      }
    );

    Assert.True(MaterialRoleRegistry.IsRole(Roles.Flux, new AssetLocation("game:lime")));
    Assert.True(
      MaterialRoleRegistry.IsRole(Roles.IronOre, new AssetLocation("game:crushed-iron-x"))
    );
  }

  [Fact]
  public void Overlay_skips_and_warns_on_a_def_missing_role_or_matcher()
  {
    var warnings = new List<string>();
    MaterialRoleLoader.Overlay(
      new[]
      {
        new MaterialRoleCatalogue
        {
          Materials = new()
          {
            new MaterialRoleDef { Role = "", Code = "game:x" }, // no role
            new MaterialRoleDef { Role = Roles.Flux }, // no code/prefix
            new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }, // valid
          },
        },
      },
      warnings.Add
    );

    Assert.Single(MaterialRoleRegistry.OfRole(Roles.Flux));
    Assert.Equal(2, warnings.Count);
  }

  [Fact]
  public void Overlay_ignores_a_catalogue_with_no_materials()
  {
    MaterialRoleLoader.Overlay(new[] { new MaterialRoleCatalogue() });
    Assert.Empty(MaterialRoleRegistry.OfRole(Roles.Flux));
  }
  #endregion

  #region JSON binding
  [Fact]
  public void MaterialRoleDef_binds_camelCase_json()
  {
    var def = JsonConvert.DeserializeObject<MaterialRoleDef>(
      @"{ ""role"": ""fuel"", ""code"": ""game:coke"", ""pathPrefix"": ""crushed-iron"", ""value"": 2 }"
    )!;

    Assert.Equal("fuel", def.Role);
    Assert.Equal("game:coke", def.Code);
    Assert.Equal("crushed-iron", def.PathPrefix);
    Assert.Equal(2f, def.Value);
  }

  [Fact]
  public void MaterialRoleDef_minimal_json_leaves_the_optionals_null()
  {
    var def = JsonConvert.DeserializeObject<MaterialRoleDef>(
      @"{ ""role"": ""flux"", ""code"": ""game:lime"" }"
    )!;

    Assert.Equal("flux", def.Role);
    Assert.Equal("game:lime", def.Code);
    Assert.Null(def.PathPrefix);
    Assert.Null(def.Value);
  }

  [Fact]
  public void MaterialRoleCatalogue_binds_the_materials_array()
  {
    var cat = JsonConvert.DeserializeObject<MaterialRoleCatalogue>(
      @"{ ""materials"": [
          { ""role"": ""flux"", ""code"": ""game:lime"" },
          { ""role"": ""ironore"", ""pathPrefix"": ""crushed-iron"" } ] }"
    )!;

    Assert.NotNull(cat.Materials);
    Assert.Equal(2, cat.Materials!.Count);
    Assert.Equal("flux", cat.Materials[0].Role);
    Assert.Equal("crushed-iron", cat.Materials[1].PathPrefix);
  }
  #endregion
}
