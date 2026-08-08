using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExpandedLib.Materials;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Registers a <c>fuel</c> grant carrying <b>no</b> <see cref="MaterialRoleDef.Value"/> - the shape a
/// content mod (or a hurried JSON patch) produces when it grants the role and forgets the number - so
/// <see cref="FuelRoleGrantTests.A_fuel_grant_with_no_Value_burns_at_the_registry_fallback_not_at_cokes"/>
/// has one to price.
/// <para>
/// <b>This may not be done from a test body, and the reason is not style.</b>
/// <see cref="MaterialRoleRegistry"/> is a process-wide store built on a plain
/// <c>Dictionary</c>/<c>List</c> with no locking, and xUnit runs test classes in parallel: a
/// <c>Register</c> on one thread while a furnace test on another is inside <c>foreach (def in list)</c>
/// throws "collection was modified" out of a test that has nothing to do with material roles. A module
/// initializer runs once, before any test in the assembly, so the write lands while nothing is reading.
/// <c>Clear()</c> + re-seed around the test is worse still - the window in which the registry is empty
/// is a window in which every furnace in the suite classifies nothing.
/// </para>
/// <para>
/// It seeds first rather than assuming <c>ModuleInit</c> already ran: module initializers have no
/// specified order between them, and <c>MaterialRoleSeeds.SeedIwexDefaults</c> opens with a
/// <c>Clear()</c> that would wipe the probe if it landed second. The seed is idempotent, so calling it
/// here is free when it has already run and correct when it has not.
/// </para>
/// </summary>
internal static class FuelRoleProbe
{
  /// <summary>A deliberately un-item-like code under a domain no mod ships, so nothing can ever resolve
  /// it to a real collectible and no other case in the suite can match it by accident.</summary>
  internal const string Code = "iwextest:fuel-with-no-value";

  [ModuleInitializer]
  internal static void Register()
  {
    // This suite's own process only, and the guard is the other half of the race argument above.
    // LowPressureExpanded.Tests references this assembly (for PipeTestWorld), so this initializer also
    // fires over there - but late, at the first touch of an iwex test type, which is mid-run on a worker
    // thread. A Register at that moment is exactly the unsynchronised write this whole comment exists to
    // avoid, landing in a suite that has no use for the probe. Derived from the assembly's own name, so a
    // project rename cannot leave it silently disabled here: the probe would simply be absent and
    // A_fuel_grant_with_no_Value... would fail, which is the loud direction.
    string home = typeof(FuelRoleProbe).Assembly.GetName().Name!;
    if (
      !AppContext.BaseDirectory.Replace('\\', '/')
        .Contains("/" + home + "/", StringComparison.Ordinal)
    )
      return;

    MaterialRoleSeeds.SeedIwexDefaults();
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Fuel, Code = Code }
    );
  }
}

/// <summary>
/// <b>The laws that hold for <em>every</em> <c>fuel</c>-role grant, enumerated rather than listed.</b>
/// <para>
/// Charcoal is the shaft's second priced fuel, and everything that made that possible is
/// open-ended by design: the shaft accepts a fuel because the registry grants it the role, prices it off
/// that grant's <c>value</c>, and names it off the resolved item. Nothing anywhere enumerates "coke and
/// charcoal". That is the right shape - a third fuel needs no new field, branch or lang key - and it is
/// also exactly why the drift here is silent: a grant is one JSON row, and a row costs nothing to add.
/// </para>
/// <para>
/// <b>The trap these guard is a tidy-up that looks obvious.</b> The firebox already burns bituminous and
/// anthracite, so granting them <c>Roles.Fuel</c> reads as removing a duplicate taxonomy. It is not: the
/// firebox asks "will this carry a heat", the shaft asks "is this a carbon reductant that survives being
/// under a burden column", and raw coal crushes to dust under one -
/// <c>docs/design/processes/coking.md</c> forbids it in a shaft, which is the entire reason the coke oven
/// exists. Granting the role would silently make raw coal chargeable into a blast furnace, because
/// <see cref="BlockEntityShaftFurnace.IsChargeItem"/> accepts anything holding it. The two taxonomies stay
/// separate on purpose, and the theory below is what makes an accidental merge fail loudly.
/// </para>
/// </summary>
public class FuelRoleGrantTests
{
  #region Every fuel grant is a complete fuel

  /// <summary>
  /// Every <c>fuel</c> grant in the registry, as the material code a charge column would store for it.
  /// <para>
  /// A prefix grant contributes its own prefix, which matches itself by construction - so a fuel granted
  /// by <see cref="MaterialRoleDef.PathPrefix"/> (none today) is still enumerated rather than skipped.
  /// Skipping it would leave the one grant shape nothing else in the suite covers uncovered.
  /// </para>
  /// </summary>
  public static IEnumerable<object[]> FuelGrants() =>
    MaterialRoleRegistry
      .OfRole(Roles.Fuel)
      .Select(def => def.Code ?? def.PathPrefix!)
      .Where(code => !string.IsNullOrEmpty(code))
      .Distinct(StringComparer.Ordinal)
      .Select(code => new object[] { code });

  /// <summary>
  /// <b>Granting <c>Roles.Fuel</c> is granting a shaft-charge permit</b>, so the grant has to hold up at
  /// every station of the path a fuel takes: into the hopper, down the column as its own band, and into the
  /// raceway as priced carbon. A grant that satisfies only some of these is a machine that accepts a fuel
  /// it cannot burn (or burns one it will not accept) - and every one of those failures is silent, because
  /// each answer is individually plausible.
  /// </summary>
  [Theory]
  [MemberData(nameof(FuelGrants))]
  public void Every_fuel_role_grant_is_chargeable_and_burnable_end_to_end(
    string code
  )
  {
    // The shaft, not the base core: the base charges burden only. A fuel is chargeable because the shaft
    // widens IsChargeCode to "burden or fuel", which is the clause this asserts is still reached.
    var shaft = new BlockEntityBlastFurnaceCold();

    Assert.True(
      BlockEntityFurnaceCore.IsFuelCode(code),
      $"'{code}' is granted the fuel role but IsFuelCode denies it"
    );
    Assert.True(
      shaft.IsChargeCode(code),
      $"'{code}' is a fuel the shaft cannot charge - the hopper would refuse it at the tank"
    );

    // And it must not be burden. The two are laid in a fixed order (fuel, then burden) and priced by
    // opposite rules; a code answering yes to both would be laid as either and read as both, which is a
    // double-count with no symptom.
    Assert.False(
      Burden.IsCode(code),
      $"'{code}' reads as both fuel and burden - the band order and the carbon accounting both break"
    );

    // Priced, not merely permitted. Zero carbon is the value a non-fuel gets, so a grant that lands on
    // it is a band that occupies a column, blocks the next fuel course by the band-order rule, and
    // contributes nothing at the raceway - a shaft that fills up and will not light.
    Assert.True(
      BlockEntityFurnaceCore.CarbonPerUnit(code) > 0f,
      $"'{code}' is charged as fuel but carries no carbon - it would fill the shaft and never burn"
    );
  }

  #endregion

  #region A forgotten value is charcoal, never coke

  [Fact]
  public void A_fuel_grant_with_no_Value_burns_at_the_registry_fallback_not_at_cokes()
  {
    // <b>The safe direction, pinned.</b> MaterialRoleRegistry.ValueOf falls back to 1.0 for a def that
    // matches but carries no value, and CarbonPerUnit divides by IwexValues.BfFuelCarbonReference - coke's
    // own 2.0. So a grant that forgets its number is worth half of coke: silently charcoal.
    //
    // The mutation this exists to catch is CarbonPerUnit passing the reference (or coke's value) as its
    // own fallback, which reads perfectly sensibly - "an unpriced fuel is the standard fuel" - and hands
    // every careless mod a free 2x on carbon. A furnace that runs hotter than the charge justifies has no
    // symptom a player can attribute; one that runs cooler is a charge they can see and fix.
    float probe = BlockEntityFurnaceCore.CarbonPerUnit(FuelRoleProbe.Code);

    Assert.Equal(0.5f, probe, 4);
    Assert.Equal(
      MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(FuelRoleProbe.Code))
        / IwexValues.BfFuelCarbonReference,
      probe,
      4
    );

    // Stated against the two real grants as well, because the constants above could both move together
    // and leave 0.5 true while the relation - the only thing that matters - had inverted.
    Assert.Equal(BlockEntityFurnaceCore.CarbonPerUnit("game:charcoal"), probe, 4);
    Assert.Equal(BlockEntityFurnaceCore.CarbonPerUnit("game:coke") / 2f, probe, 4);
    Assert.True(probe < BlockEntityFurnaceCore.CarbonPerUnit("game:coke"));

    // And the fallback is a property of the registry, not of this one def: the same 1.0 a valueless flux
    // or scrap grant gets. Read off a real shipped valueless row so the number cannot be pinned here while
    // having quietly moved there.
    Assert.Equal(1f, MaterialRoleRegistry.ValueOf(Roles.Flux, new AssetLocation("game:lime")), 4);
  }

  #endregion

  #region The seed mirrors the shipped asset

  /// <summary>
  /// The role tokens to compare across. The <see cref="Roles"/> constants (a mod may invent its own role
  /// string, so the shipped file's own roles are unioned in) - which is what makes this a set comparison
  /// rather than a spot check on the roles someone remembered to list.
  /// </summary>
  private static IEnumerable<string> RoleTokens(IEnumerable<MaterialRoleDef> shipped) =>
    typeof(Roles)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(f => f.IsLiteral && f.FieldType == typeof(string))
      .Select(f => (string)f.GetRawConstantValue()!)
      .Concat(shipped.Select(d => d.Role))
      .Select(r => r.ToLowerInvariant())
      .Distinct(StringComparer.Ordinal);

  /// <summary>One role assignment as a comparable line. Codes are domain-normalised (the registry matches
  /// that way, so <c>lime</c> and <c>game:lime</c> are the same row), and a missing value is spelled
  /// <c>-</c> rather than defaulted - "no value" is a distinct fact from "value 1".</summary>
  private static string Row(MaterialRoleDef def) =>
    string.Join(
      " | ",
      def.Role.ToLowerInvariant(),
      def.Code is { Length: > 0 } c ? new AssetLocation(c).ToString() : "",
      def.PathPrefix ?? "",
      def.Value.HasValue
        ? def.Value.Value.ToString("0.####", CultureInfo.InvariantCulture)
        : "-"
    );

  [Fact]
  public void Seed_matches_the_shipped_materialroles_json()
  {
    // <b>The headless registry and the shipped file are two hand-written copies of one table.</b>
    // MaterialRoleSeeds exists because the test harness has no asset pipeline, its doc has always claimed
    // it mirrors assets/iwex/config/materialroles.json "entry-for-entry", and nothing asserted it - which
    // is how `game:metalbit-steel` shipped as scrap in the game while headless steel bits simply were not
    // scrap for weeks. A forgotten fuel row is the same gap with teeth: every furnace test in the suite
    // would burn at a rate no world ever sees, and pass.
    //
    // Read from the source tree (the goldens' own repo-root walk), not from a copied-to-output asset:
    // there is no copy step for this file, and adding one would just create a third thing to go stale.
    string path = DefinitionGoldens.SolutionRelative(
      "assets/iwex/config/materialroles.json"
    );
    Assert.True(File.Exists(path), $"missing shipped material roles at {path}");

    List<MaterialRoleDef> shipped =
      JsonConvert
        .DeserializeObject<MaterialRoleCatalogue>(File.ReadAllText(path))
        ?.Materials
      ?? throw new InvalidOperationException($"no 'materials' array in {path}");

    List<string> fromAsset = shipped.Select(Row).OrderBy(r => r, StringComparer.Ordinal).ToList();
    List<string> fromSeed = RoleTokens(shipped)
      .SelectMany(MaterialRoleRegistry.OfRole)
      // The one exclusion, and it is a fixture of this very file rather than a softening of the claim:
      // `Probe` registers a synthetic valueless fuel grant at module load (see its remarks for why it
      // cannot be registered and removed around a test). It is not in the shipped file and must not be.
      .Where(def => def.Code != FuelRoleProbe.Code)
      .Select(Row)
      .OrderBy(r => r, StringComparer.Ordinal)
      .ToList();

    // Set equality, in both directions at once. A one-way "every asset row is seeded" check would miss
    // a seed row that no longer ships - the drift that makes headless tests green on a rule the game does
    // not have.
    Assert.Equal(fromAsset, fromSeed);
  }

  #endregion
}
