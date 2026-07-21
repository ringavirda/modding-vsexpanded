using ExpandedLib.Materials;
using IronworkingExpanded.Compat;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity guard for the ore mixer's + furnace core's material classification after it moved from
/// hardcoded <c>Collectible.Code.Path</c> equality onto <see cref="MaterialRoleRegistry"/>. For every
/// item the machines see, the registry predicate the migrated code now calls must return the exact same
/// result as the old inline string check. Relies on the module-init-seeded iwex roles
/// (<c>MaterialRoleSeeds.SeedIwexDefaults</c>), the headless stand-in for materialroles.json; read-only,
/// so it never disturbs the shared registry.
/// </summary>
public class MixerRoleParityTests
{
  // The paths the mixer classifies, plus non-inputs, exercising both the match and miss branches.
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
  public void Flux_role_matches_the_old_lime_check(string path)
  {
    bool old = path == "lime";
    bool @new = MaterialRoleRegistry.IsRole(Roles.Flux, new AssetLocation(path));
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
  public void Scrap_role_matches_the_old_metalbit_iron_check(string path)
  {
    bool old = path == "metalbit-iron";
    bool @new = MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(path));
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
  public void Fuel_role_matches_the_old_coke_or_charcoal_check(string path)
  {
    bool old = path == "coke" || path == "charcoal";
    bool @new = MaterialRoleRegistry.IsRole(Roles.Fuel, new AssetLocation(path));
    Assert.Equal(old, @new);
  }

  [Theory]
  [InlineData("crushed-iron")]
  [InlineData("crushed-iron-magnetite")]
  [InlineData("crushed-hematite")] // mod ore, but no mod enabled headless → not iron, like before
  [InlineData("coke")]
  [InlineData("lime")]
  [InlineData("metalbit-iron")]
  public void IronOre_role_matches_the_old_crushed_iron_prefix_check(string path)
  {
    // With no compat mod enabled, the old IsCrushedIronOre reduced to the crushed-iron prefix.
    bool old = path.StartsWith("crushed-iron", System.StringComparison.Ordinal);
    bool @new = IronOreCompat.IsCrushedIronOre(path);
    Assert.Equal(old, @new);
  }

  [Theory]
  [InlineData("coke", 2f)] // one lump of coke = 2 fuel value
  [InlineData("charcoal", 0.5f)] // charcoal is the poorer reductant
  public void Fuel_value_matches_the_old_per_item_carbon_value(
    string path,
    float expected
  )
  {
    // The old FuelValuePerItem returned charcoal→0.5 else→2 (coke); now sourced from the fuel role.
    float @new = MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(path), 1f);
    Assert.Equal(expected, @new);
  }

  // The furnace core's charge token (Burden.IsAny || blastmix) - only the blastmix half moved to a role.
  [Theory]
  [InlineData("iwex:blastmix", true)]
  [InlineData("iwex:burden", false)] // burden is caught by the intrinsic Burden.IsAny, not the role
  [InlineData("game:lime", false)]
  [InlineData("game:coke", false)]
  public void Charge_role_matches_the_old_blastmix_token(string code, bool expected)
  {
    var stack = new ItemStack(new Item { Code = new AssetLocation(code) });

    bool old = stack.Collectible.Code.Path == "blastmix";
    bool @new = MaterialRoleRegistry.IsRole(Roles.Charge, stack);
    Assert.Equal(expected, old); // pin the oracle itself
    Assert.Equal(old, @new);
  }
}
