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
/// Registers a <c>fuel</c> grant carrying no <see cref="MaterialRoleDef.Value"/> - the shape a JSON patch
/// produces when it grants the role and omits the number - for
/// <see cref="FuelRoleGrantTests.A_fuel_grant_with_no_Value_burns_at_the_registry_fallback_not_at_cokes"/>
/// to price. Registered from a module initializer, never from a test body:
/// <see cref="MaterialRoleRegistry"/> is a process-wide unlocked store and xUnit runs test classes in
/// parallel. It seeds first because <c>MaterialRoleSeeds.SeedIwexDefaults</c> opens with a <c>Clear()</c>
/// and initializer order is unspecified; the seed is idempotent.
/// </summary>
internal static class FuelRoleProbe {
  /// <summary>An un-item-like code under a domain no mod ships, so nothing can resolve it to a real
  /// collectible and no other case in the suite can match it by accident.</summary>
  internal const string Code = "iwextest:fuel-with-no-value";

  [ModuleInitializer]
  internal static void Register() {
    // This suite's own process only. LowPressureExpanded.Tests references this assembly, so the
    // initializer would otherwise fire there mid-run on a worker thread - the unsynchronised write ruled
    // out above. The guard is derived from the assembly name, so a rename disables the probe outright.
    string home = typeof(FuelRoleProbe).Assembly.GetName().Name!;
    if (
      !AppContext
        .BaseDirectory.Replace('\\', '/')
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
/// The laws that hold for every <c>fuel</c>-role grant, enumerated from the registry rather than listed: a
/// fuel is accepted, priced and named off its grant, so a third fuel needs no new field or branch.
/// <para>
/// The firebox burns bituminous and anthracite without holding <c>Roles.Fuel</c> - the two taxonomies are
/// separate. Raw coal is deliberately not granted the role, since that would make it chargeable into a
/// blast furnace through <see cref="BlockEntityShaftFurnace.IsChargeItem"/>
/// (<c>docs/design/processes/coking.md</c>).
/// </para>
/// </summary>
public class FuelRoleGrantTests {
  #region Every fuel grant is a complete fuel

  /// <summary>
  /// Every <c>fuel</c> grant in the registry, as the material code a charge column stores. A prefix grant
  /// contributes its own prefix, which matches itself, so a fuel granted by
  /// <see cref="MaterialRoleDef.PathPrefix"/> is enumerated rather than skipped.
  /// </summary>
  public static IEnumerable<object[]> FuelGrants() =>
    MaterialRoleRegistry
      .OfRole(Roles.Fuel)
      .Select(def => def.Code ?? def.PathPrefix!)
      .Where(code => !string.IsNullOrEmpty(code))
      .Distinct(StringComparer.Ordinal)
      .Select(code => new object[] { code });

  /// <summary>
  /// Granting <c>Roles.Fuel</c> grants a shaft-charge permit, so the grant has to hold at every station of
  /// the path: into the hopper, down the column as its own band, and into the raceway as priced carbon. A
  /// grant satisfying only some of these fails silently, since each answer is individually plausible.
  /// </summary>
  [Theory]
  [MemberData(nameof(FuelGrants))]
  public void Every_fuel_role_grant_is_chargeable_and_burnable_end_to_end(
    string code
  ) {
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
    // opposite rules, so a code answering yes to both is a silent double-count.
    Assert.False(
      Burden.IsCode(code),
      $"'{code}' reads as both fuel and burden - the band order and the carbon accounting both break"
    );

    // Priced, not merely permitted. Zero carbon is what a non-fuel scores, so a grant landing on it
    // occupies a column, blocks the next fuel course and contributes nothing at the raceway.
    Assert.True(
      BlockEntityFurnaceCore.CarbonPerUnit(code) > 0f,
      $"'{code}' is charged as fuel but carries no carbon - it would fill the shaft and never burn"
    );
  }

  #endregion

  #region A forgotten value is charcoal, never coke

  [Fact]
  public void A_fuel_grant_with_no_Value_burns_at_the_registry_fallback_not_at_cokes() {
    // MaterialRoleRegistry.ValueOf falls back to 1.0 for a def that matches but carries no value, and
    // CarbonPerUnit divides by IwexValues.BfFuelCarbonReference - coke's own 2.0 - so a grant that omits
    // its number is worth half of coke, i.e. charcoal, rather than coke's own value.
    float probe = BlockEntityFurnaceCore.CarbonPerUnit(FuelRoleProbe.Code);

    Assert.Equal(0.5f, probe, 4);
    Assert.Equal(
      MaterialRoleRegistry.ValueOf(
        Roles.Fuel,
        new AssetLocation(FuelRoleProbe.Code)
      ) / IwexValues.BfFuelCarbonReference,
      probe,
      4
    );

    // Stated against the two real grants as well, because the constants above could both move together and
    // leave 0.5 true while the relation had inverted.
    Assert.Equal(
      BlockEntityFurnaceCore.CarbonPerUnit("game:charcoal"),
      probe,
      4
    );
    Assert.Equal(
      BlockEntityFurnaceCore.CarbonPerUnit("game:coke") / 2f,
      probe,
      4
    );
    Assert.True(probe < BlockEntityFurnaceCore.CarbonPerUnit("game:coke"));

    // The fallback belongs to the registry, not to this def: the same 1.0 a valueless flux grant gets.
    // Read off a real shipped valueless row so it cannot be pinned here while having moved there.
    Assert.Equal(
      1f,
      MaterialRoleRegistry.ValueOf(Roles.Flux, new AssetLocation("game:lime")),
      4
    );
  }

  #endregion

  #region The seed mirrors the shipped asset

  /// <summary>
  /// The role tokens to compare across: the <see cref="Roles"/> constants unioned with the shipped file's
  /// own roles, since a mod may invent a role string. The union makes this a set comparison rather than a
  /// spot check.
  /// </summary>
  private static IEnumerable<string> RoleTokens(
    IEnumerable<MaterialRoleDef> shipped
  ) =>
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
  public void Seed_matches_the_shipped_materialroles_json() {
    // MaterialRoleSeeds exists because the harness has no asset pipeline, and claims to mirror
    // assets/iwex/config/materialroles.json entry for entry. Read from the source tree, not from a
    // copied-to-output asset: there is no copy step for this file.
    string path = DefinitionGoldens.SolutionRelative(
      "assets/iwex/config/materialroles.json"
    );
    Assert.True(File.Exists(path), $"missing shipped material roles at {path}");

    List<MaterialRoleDef> shipped =
      JsonConvert
        .DeserializeObject<MaterialRoleCatalogue>(File.ReadAllText(path))
        ?.Materials
      ?? throw new InvalidOperationException($"no 'materials' array in {path}");

    List<string> fromAsset = shipped
      .Select(Row)
      .OrderBy(r => r, StringComparer.Ordinal)
      .ToList();
    List<string> fromSeed = RoleTokens(shipped)
      .SelectMany(MaterialRoleRegistry.OfRole)
      // The one exclusion: FuelRoleProbe registers a synthetic valueless fuel grant at module load. It is
      // not in the shipped file and must not be.
      .Where(def => def.Code != FuelRoleProbe.Code)
      .Select(Row)
      .OrderBy(r => r, StringComparer.Ordinal)
      .ToList();

    // Set equality in both directions: a one-way "every asset row is seeded" check would miss a seed row
    // that no longer ships.
    Assert.Equal(fromAsset, fromSeed);
  }

  #endregion
}
