using ExpandedLib.Materials;
using IronworkingExpanded.Compat;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity guard for iwex's material classification after it moved from hardcoded
/// <c>Collectible.Code.Path</c> equality onto <see cref="MaterialRoleRegistry"/>. For every item the
/// machines see, the registry predicate the migrated code now calls must return the exact same result as
/// the old inline string check. Relies on the module-init-seeded iwex roles
/// (<c>MaterialRoleSeeds.SeedIwexDefaults</c>), the headless stand-in for materialroles.json; read-only,
/// so it never disturbs the shared registry.
/// <para>
/// <b>Renamed from <c>MixerRoleParityTests</c></b> when the ore mixer was deleted. The
/// file survives the machine because it never guarded the machine: it guards the <em>registry</em>, and
/// the burdenmaker, the furnace core and the cupola all classify through the same predicates. The old
/// name would have been a lie about what exists - and a test whose name names nothing is one nobody
/// thinks to keep true.
/// </para>
/// </summary>
public class MaterialRoleParityTests
{
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

  /// <summary>
  /// <b>Not a parity case, and it must not be folded into one.</b> The <c>scrap</c> role widened
  /// to carry the pig family, so its old oracle - <c>path == "metalbit-iron"</c> - is no
  /// longer the answer for these three and never will be again. Adding them to the theory above would have
  /// forced its oracle to be relaxed into "whatever the registry says", which is a tautology dressed as a
  /// parity guard.
  /// <para>
  /// This is load-bearing rather than bookkeeping: the cupola charges <c>Roles.Scrap</c> directly now
  /// that the ore mixer is gone, so if these grants go missing the cupola accepts nothing and <b>cast iron
  /// has no source in survival</b> - with every furnace test still green, because they charge by code.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("iwex:pig")]
  [InlineData("iwex:pigchunk")]
  [InlineData("iwex:pigbit")]
  [InlineData("game:metalbit-iron")]
  [InlineData("game:metalbit-steel")]
  public void The_scrap_role_covers_every_metal_a_cupola_remelts(string code)
  {
    Assert.True(MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(code)));
  }

  [Theory]
  [InlineData("iwex:burden")] // prepared ore charge - the blast furnace's food, never the cupola's
  [InlineData("game:coke")]
  [InlineData("game:lime")]
  [InlineData("game:ingot-iron")] // a whole ingot is stock, not scrap; only bits and pig are
  public void The_scrap_role_stops_where_it_should(string code)
  {
    Assert.False(MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(code)));
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
  // Retuned 0.5 → 1.0, and this is no longer a *parity* row. It was the old per-item
  // carbon value - 4 charcoal to a lump of coke - and the design now puts two charcoal to a lump.
  //
  // The number moved because its owner moved. `BlockEntityFurnaceCore.CarbonPerUnit` reads it to price a
  // shaft furnace's raceway burn, where charcoal had been burning at coke's exact rate for free; and the ore
  // mixer - the only other reader - was deleted along with the burden fuel stamp
  // itself, so fuel is now charged directly at the furnace's hopper as its own bands.
  //
  // That deletion has landed, which is what makes this row unambiguous: it is the raceway's ratio, with
  // exactly one reader, rather than a compromise between two machines that disagreed.
  [InlineData("charcoal", 1f)] // charcoal is the poorer reductant - two to a lump of coke
  public void Fuel_value_matches_the_old_per_item_carbon_value(
    string path,
    float expected
  )
  {
    // The old FuelValuePerItem returned charcoal→0.5 else→2 (coke); now sourced from the fuel role.
    float @new = MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(path), 1f);
    Assert.Equal(expected, @new);
  }

  // `Charge_role_matches_the_old_blastmix_token` was retired here, not edited.
  //
  // Its oracle was the literal `stack.Collectible.Code.Path == "blastmix"` - the pre-role check the
  // `charge` role replaced. The item is gone and so is the role's only grant, so the oracle names
  // nothing: keeping the case green by changing its expectation to `false` everywhere would have turned
  // a genuine parity guard into a tautology (`false == false` for four inputs), which is strictly worse
  // than no test, because it still reads like coverage.
  //
  // What the case was protecting has no successor to protect: prepared burden is recognised by its own
  // item identity (`Burden.IsAny`) and coke by the `fuel` role, and both of those are pinned above and in
  // `ChargeCodeGateTests`. The parity cases for flux / scrap / fuel are untouched - their oracles are
  // still live code paths.
}
