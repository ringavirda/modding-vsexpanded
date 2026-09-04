using System.Linq;
using ExpandedLib.Materials;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The process-wide material-role registry the mixer, hoppers and furnace core classify by: the two
/// match modes (exact domain-normalised <see cref="MaterialRoleDef.Code"/> and domain-blind
/// <see cref="MaterialRoleDef.PathPrefix"/>), the per-role <see cref="MaterialRoleDef.Value"/> lookup,
/// null handling, and the code-contributor seam. The registry is a static store, so each test clears
/// it first.
/// </summary>
[Collection("MaterialRoles")] // process-wide static; serialize the mutating classes
public class MaterialRoleRegistryTests {
  public MaterialRoleRegistryTests() {
    MaterialRoleRegistry.Clear();
    MaterialRoleRegistry.ClearContributors();
  }

  #region Code match (exact, domain-normalised)
  [Fact]
  public void Code_match_is_exact_and_domain_normalised() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }
    );

    Assert.True(
      MaterialRoleRegistry.IsRole(Roles.Flux, new AssetLocation("game:lime"))
    );
    // A bare code defaults to the game domain, so it keys the same entry.
    Assert.True(
      MaterialRoleRegistry.IsRole(Roles.Flux, new AssetLocation("lime"))
    );
    // Wrong code, and the right code under a different role, both miss.
    Assert.False(
      MaterialRoleRegistry.IsRole(Roles.Flux, new AssetLocation("game:coke"))
    );
    Assert.False(
      MaterialRoleRegistry.IsRole(Roles.Fuel, new AssetLocation("game:lime"))
    );
  }

  [Fact]
  public void Code_match_is_domain_sensitive_for_a_non_game_code() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Charge, Code = "iiex:blastmix" }
    );

    Assert.True(
      MaterialRoleRegistry.IsRole(
        Roles.Charge,
        new AssetLocation("iiex:blastmix")
      )
    );
    // A different domain with the same path is a different item - it must not match.
    Assert.False(
      MaterialRoleRegistry.IsRole(
        Roles.Charge,
        new AssetLocation("game:blastmix")
      )
    );
  }
  #endregion

  #region PathPrefix match (domain-blind StartsWith)
  [Theory]
  [InlineData("game:crushed-iron", true)]
  [InlineData("game:crushed-iron-magnetite", true)]
  [InlineData("othermod:crushed-iron-ore", true)] // domain-blind: only the path matters
  [InlineData("game:crushed-hematite", false)]
  [InlineData("game:coke", false)]
  public void PathPrefix_matches_the_path_regardless_of_domain(
    string code,
    bool expected
  ) {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" }
    );

    Assert.Equal(
      expected,
      MaterialRoleRegistry.IsRole(Roles.IronOre, new AssetLocation(code))
    );
  }
  #endregion

  #region Value
  [Fact]
  public void ValueOf_returns_the_matched_defs_value() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef {
        Role = Roles.Fuel,
        Code = "game:coke",
        Value = 2f,
      }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef {
        Role = Roles.Fuel,
        Code = "game:charcoal",
        Value = 0.5f,
      }
    );

    Assert.Equal(
      2f,
      MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation("game:coke"))
    );
    Assert.Equal(
      0.5f,
      MaterialRoleRegistry.ValueOf(
        Roles.Fuel,
        new AssetLocation("game:charcoal")
      )
    );
  }

  [Fact]
  public void ValueOf_of_an_unmatched_item_returns_the_fallback() {
    // Empty registry: the default fallback, and a caller-supplied one.
    Assert.Equal(
      1f,
      MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation("game:coke"))
    );
    Assert.Equal(
      7f,
      MaterialRoleRegistry.ValueOf(
        Roles.Fuel,
        new AssetLocation("game:coke"),
        7f
      )
    );
  }

  [Fact]
  public void ValueOf_of_a_matched_def_without_a_value_returns_the_fallback() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Fuel, Code = "game:peat" } // no value
    );

    Assert.Equal(
      1f,
      MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation("game:peat"))
    );
  }
  #endregion

  #region ItemStack overloads + null safety
  [Fact]
  public void IsRole_reads_a_stacks_collectible_code() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }
    );
    var stack = new ItemStack(
      new Item { Code = new AssetLocation("game:lime") }
    );

    Assert.True(MaterialRoleRegistry.IsRole(Roles.Flux, stack));
    Assert.Equal(2f, MaterialRoleRegistry.ValueOf(Roles.Flux, stack, 2f)); // no value → fallback
  }

  [Fact]
  public void IsRole_and_ValueOf_are_null_safe() {
    Assert.False(MaterialRoleRegistry.IsRole(Roles.Flux, (ItemStack?)null));
    Assert.False(MaterialRoleRegistry.IsRole(Roles.Flux, (AssetLocation)null!));
    Assert.Equal(
      3f,
      MaterialRoleRegistry.ValueOf(Roles.Fuel, (ItemStack?)null, 3f)
    );
  }
  #endregion

  #region Register guards + OfRole
  [Fact]
  public void Register_ignores_a_def_with_no_role_or_no_matcher() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = "", Code = "game:x" } // no role
    );
    MaterialRoleRegistry.Register(new MaterialRoleDef { Role = Roles.Flux }); // no matcher

    Assert.Empty(MaterialRoleRegistry.OfRole(Roles.Flux));
    Assert.Empty(MaterialRoleRegistry.OfRole(""));
  }

  [Fact]
  public void OfRole_returns_the_defs_registered_under_a_role() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Fuel, Code = "game:coke" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Fuel, Code = "game:charcoal" }
    );

    Assert.Equal(2, MaterialRoleRegistry.OfRole(Roles.Fuel).Count());
    Assert.Empty(MaterialRoleRegistry.OfRole(Roles.Scrap));
  }

  [Fact]
  public void Clear_empties_the_registry() {
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }
    );
    Assert.Single(MaterialRoleRegistry.OfRole(Roles.Flux));

    MaterialRoleRegistry.Clear();
    Assert.Empty(MaterialRoleRegistry.OfRole(Roles.Flux));
  }
  #endregion

  #region Contributors
  [Fact]
  public void InvokeContributors_runs_every_registered_contributor() {
    int ran = 0;
    ICoreAPI? seen = null;
    var api = Substitute.For<ICoreAPI>();
    MaterialRoleRegistry.RegisterContributor(_ => ran++);
    MaterialRoleRegistry.RegisterContributor(a => {
      ran++;
      seen = a; // the api is threaded through to each contributor
    });

    MaterialRoleRegistry.InvokeContributors(api);
    Assert.Equal(2, ran);
    Assert.Same(api, seen);
  }

  [Fact]
  public void ClearContributors_removes_them() {
    int ran = 0;
    MaterialRoleRegistry.RegisterContributor(_ => ran++);
    MaterialRoleRegistry.ClearContributors();

    MaterialRoleRegistry.InvokeContributors(Substitute.For<ICoreAPI>());
    Assert.Equal(0, ran);
  }
  #endregion
}
