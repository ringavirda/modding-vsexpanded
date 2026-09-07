using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Materials;
using IronIndustryExpanded.Compat;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Parity guard for iiex's material classification: for every item the machines see, the
/// <see cref="MaterialRoleRegistry"/> predicate must return the same result as the
/// <c>Collectible.Code.Path</c> equality check it replaced. Reads the module-init-seeded iiex roles
/// (<c>MaterialRoleSeeds.SeedIiexDefaults</c>), the headless stand-in for materialroles.json; read-only,
/// so the shared registry is never disturbed.
/// </summary>
public class MaterialRoleParityTests {
  // The paths these roles classify, plus non-inputs, exercising both the match and miss branches.
  [Theory]
  [InlineData("lime")]
  [InlineData("coke")]
  [InlineData("charcoal")]
  [InlineData("metalbit-iron")]
  [InlineData("metalbit-copper")]
  [InlineData("blastmix")]
  [InlineData("crushed-iron")]
  [InlineData("crushed-iron-magnetite")]
  [InlineData("crushed-hematite")]
  [InlineData("ingot-iron")]
  [InlineData("stick")]
  public void Flux_role_matches_the_old_lime_check(string path) {
    bool old = path == "lime";
    bool @new = MaterialRoleRegistry.IsRole(
      Roles.Flux,
      new AssetLocation(path)
    );
    Assert.Equal(old, @new);
  }

  [Theory]
  [InlineData("lime")]
  [InlineData("coke")]
  [InlineData("charcoal")]
  [InlineData("metalbit-iron")]
  [InlineData("metalbit-copper")]
  [InlineData("blastmix")]
  [InlineData("crushed-iron")]
  [InlineData("ingot-iron")]
  [InlineData("stick")]
  public void Scrap_role_matches_the_old_metalbit_iron_check(string path) {
    bool old = path == "metalbit-iron";
    bool @new = MaterialRoleRegistry.IsRole(
      Roles.Scrap,
      new AssetLocation(path)
    );
    Assert.Equal(old, @new);
  }

  [Theory]
  [InlineData("lime")]
  [InlineData("coke")]
  [InlineData("charcoal")]
  [InlineData("metalbit-iron")]
  [InlineData("blastmix")]
  [InlineData("crushed-iron")]
  [InlineData("stick")]
  public void Fuel_role_matches_the_old_coke_or_charcoal_check(string path) {
    bool old = path == "coke" || path == "charcoal";
    bool @new = MaterialRoleRegistry.IsRole(
      Roles.Fuel,
      new AssetLocation(path)
    );
    Assert.Equal(old, @new);
  }

  /// <summary>
  /// Not a parity case: the <c>scrap</c> role covers the pig family, for which the
  /// <c>path == "metalbit-iron"</c> oracle above does not answer. The cupola charges <c>Roles.Scrap</c>
  /// directly, so a missing grant leaves cast iron with no source in survival while the furnace suites
  /// stay green - they charge by code.
  /// </summary>
  [Theory]
  [InlineData("iiex:pig")]
  [InlineData("iiex:pigchunk")]
  [InlineData("iiex:pigbit")]
  [InlineData("game:metalbit-iron")]
  [InlineData("game:metalbit-steel")]
  public void The_scrap_role_covers_every_metal_a_cupola_remelts(string code) {
    Assert.True(
      MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(code))
    );
  }

  [Theory]
  [InlineData("iiex:burden")] // prepared ore charge for the blast furnace, never the cupola
  [InlineData("game:coke")]
  [InlineData("game:lime")]
  [InlineData("game:ingot-iron")] // a whole ingot is stock, not scrap; only bits and pig are
  public void The_scrap_role_stops_where_it_should(string code) {
    Assert.False(
      MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(code))
    );
  }

  [Theory]
  [InlineData("crushed-iron")]
  [InlineData("crushed-iron-magnetite")]
  [InlineData("crushed-hematite")] // mod ore; no compat mod is enabled headless, so it is not iron
  [InlineData("coke")]
  [InlineData("lime")]
  [InlineData("metalbit-iron")]
  public void IronOre_role_matches_the_old_crushed_iron_prefix_check(
    string path
  ) {
    // With no compat mod enabled, IsCrushedIronOre reduces to the crushed-iron prefix.
    bool old = path.StartsWith("crushed-iron", System.StringComparison.Ordinal);
    bool @new = IronOreCompat.IsCrushedIronOre(path);
    Assert.Equal(old, @new);
  }

  [Theory]
  // Fuel value is carbon per item. BlockEntityFurnaceCore.CarbonPerUnit reads it to price a shaft
  // furnace's raceway burn, and is the only reader. See docs/design/items/fuels.md.
  [InlineData("coke", 2f)]
  [InlineData("charcoal", 1f)] // the poorer reductant: two charcoal to a lump of coke
  public void Fuel_value_matches_the_old_per_item_carbon_value(
    string path,
    float expected
  ) {
    float @new = MaterialRoleRegistry.ValueOf(
      Roles.Fuel,
      new AssetLocation(path),
      1f
    );
    Assert.Equal(expected, @new);
  }
}
