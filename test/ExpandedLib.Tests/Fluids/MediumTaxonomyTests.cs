using System;
using System.IO;
using System.Linq;
using ExpandedLib.Fluids;
using Newtonsoft.Json;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The medium taxonomy that replaced the hardcoded <c>PipeNetworkState</c> string helpers. The
/// load-bearing guarantee is <b>parity</b>: for the four built-in media, <see cref="IMediumTaxonomy"/>
/// must reproduce the old <c>MediaCompatible</c> / <c>GetHigherPriorityGas</c> / <c>== "Water"</c>
/// truth tables exactly, so no pipe run behaves differently. The one intended divergence - two
/// different liquids no longer silently blending - is pinned separately. <see cref="ExLiquids"/> is a
/// process-wide static, so the class is serialized and reset to the built-in seed before each test.
/// </summary>
[Collection("ExLiquids")]
public class MediumTaxonomyTests
{
  private static readonly string[] AllMedia = ["", "Air", "Steam", "Exhaust", "Water"];
  private static readonly string[] Gases = ["Air", "Steam", "Exhaust"];

  public MediumTaxonomyTests()
  {
    ExLiquids.Clear();
    ExLiquids.SeedDefaults();
  }

  // The exact pre-registry formulas, as the parity oracle.
  private static bool OldCompatible(string current, string medium) =>
    current.Length == 0 || (current == "Water") == (medium == "Water");

  private static string OldHigherPriority(string a, string b)
  {
    if (a == "Exhaust" || b == "Exhaust")
      return "Exhaust";
    if (a == "Steam" || b == "Steam")
      return "Steam";
    return "Air";
  }

  #region Parity with the old string helpers
  [Fact]
  public void Compatible_matches_the_old_truth_table_for_every_builtin_pair()
  {
    foreach (string current in AllMedia)
      foreach (string medium in AllMedia)
        Assert.Equal(
          OldCompatible(current, medium),
          ExLiquids.Taxonomy.Compatible(current, medium)
        );
  }

  [Fact]
  public void HigherPriority_matches_the_old_gas_ranking_both_orders()
  {
    foreach (string a in Gases)
      foreach (string b in Gases)
        Assert.Equal(
          OldHigherPriority(a, b),
          ExLiquids.Taxonomy.HigherPriority(a, b)
        );
  }

  [Theory]
  [InlineData("Water", true)]
  [InlineData("Air", false)]
  [InlineData("Steam", false)]
  [InlineData("Exhaust", false)]
  [InlineData("", false)] // unclaimed run reads as not-liquid (old == "Water")
  [InlineData("Nonsense", false)] // unknown code degrades to gas, never throws
  public void IsLiquid_matches_the_old_water_only_rule(string code, bool expected)
  {
    Assert.Equal(expected, ExLiquids.Taxonomy.IsLiquid(code));
  }
  #endregion

  #region The intended divergence + condensation
  [Fact]
  public void Two_different_liquids_do_not_blend()
  {
    // The old MediaCompatible treated ALL non-water as one family, so a second liquid would silently
    // blend with water. The registry fixes that: a liquid mixes only with the same liquid code.
    ExLiquids.Register(new LiquidDef { Code = "Oil", Phase = LiquidPhase.Liquid });

    Assert.True(ExLiquids.Taxonomy.IsLiquid("Oil"));
    Assert.False(ExLiquids.Taxonomy.Compatible("Water", "Oil"));
    Assert.False(ExLiquids.Taxonomy.Compatible("Oil", "Water"));
    Assert.True(ExLiquids.Taxonomy.Compatible("Oil", "Oil"));
    Assert.False(ExLiquids.Taxonomy.Compatible("Oil", "Air")); // liquid vs gas
  }

  [Fact]
  public void Steam_condenses_to_water_only_below_its_boil_point()
  {
    Assert.True(
      ExLiquids.Taxonomy.TryCondensation("Steam", 90f, out string target, out _)
    );
    Assert.Equal("Water", target);

    Assert.False(ExLiquids.Taxonomy.TryCondensation("Steam", 100f, out _, out _)); // at boil, not below
    Assert.False(ExLiquids.Taxonomy.TryCondensation("Steam", 130f, out _, out _));
    Assert.False(ExLiquids.Taxonomy.TryCondensation("Air", 20f, out _, out _)); // no condensation
    Assert.False(ExLiquids.Taxonomy.TryCondensation("Water", 5f, out _, out _)); // no target
  }
  #endregion

  #region Vaporisation + the temp-independent phase-change pair (the general-still substrate)
  [Fact]
  public void Water_vaporises_to_steam_only_at_or_above_its_boil_point()
  {
    // The mirror of condensation: gates at/above the boil point (condensation gates below the dew point).
    Assert.True(
      ExLiquids.Taxonomy.TryVaporisation("Water", 100f, out string target, out _)
    );
    Assert.Equal("Steam", target);

    Assert.True(ExLiquids.Taxonomy.TryVaporisation("Water", 130f, out _, out _)); // above boil
    Assert.False(ExLiquids.Taxonomy.TryVaporisation("Water", 99f, out _, out _)); // below boil
    Assert.False(ExLiquids.Taxonomy.TryVaporisation("Steam", 200f, out _, out _)); // a gas has no boil pair
    Assert.False(ExLiquids.Taxonomy.TryVaporisation("Air", 500f, out _, out _)); // no vaporisesTo
  }

  [Fact]
  public void Water_and_steam_are_reciprocal_phase_change_partners()
  {
    // The seeded pair: Water boils to Steam, Steam condenses to Water. The general still reads exactly
    // this data per fraction; the boiler is its degenerate single-fraction case.
    Assert.True(
      ExLiquids.Taxonomy.VaporisationTarget("Water", out string gas, out _)
    );
    Assert.Equal("Steam", gas);
    Assert.True(
      ExLiquids.Taxonomy.CondensationTarget("Steam", out string liquid, out _)
    );
    Assert.Equal("Water", liquid);
  }

  [Fact]
  public void CondensationTarget_is_temperature_independent()
  {
    // An active condenser supplies its own cooling, so the pair lookup must NOT gate on the gas's
    // current temperature - a 150C steam line still condenses (exactly what the condenser BE relies on,
    // and what a temp-gated lookup would wrongly refuse).
    Assert.True(
      ExLiquids.Taxonomy.CondensationTarget("Steam", out string target, out _)
    );
    Assert.Equal("Water", target);
    Assert.False(ExLiquids.Taxonomy.TryCondensation("Steam", 150f, out _, out _));
  }

  [Fact]
  public void Builtin_phase_change_leaves_the_volume_factor_to_the_caller()
  {
    // The built-ins leave the factor 0 (null in the def) so exlib carries no ppex expansion constant;
    // consumers fall back to their own default (the condenser to PpexValues.SteamExpansionFactor).
    ExLiquids.Taxonomy.CondensationTarget("Steam", out _, out float condFactor);
    ExLiquids.Taxonomy.VaporisationTarget("Water", out _, out float vapFactor);
    Assert.Equal(0f, condFactor);
    Assert.Equal(0f, vapFactor);
  }

  [Fact]
  public void A_fraction_can_ship_its_own_phase_change_pair_and_factor()
  {
    // A distillation add-on registers a fraction with an explicit boil/condense pair + factor; a still
    // reads them straight from the taxonomy, no code change - the substrate the staged plan builds on.
    ExLiquids.Register(
      new LiquidDef
      {
        Code = "Benzene",
        Phase = LiquidPhase.Liquid,
        VaporisesTo = "BenzeneVapour",
        BoilPointC = 80f,
        VaporiseVolumeFactor = 12f,
      }
    );
    ExLiquids.Register(
      new LiquidDef
      {
        Code = "BenzeneVapour",
        Phase = LiquidPhase.Gas,
        CondensesTo = "Benzene",
        CondenseBelowC = 80f,
        CondenseVolumeFactor = 12f,
      }
    );

    Assert.True(
      ExLiquids.Taxonomy.TryVaporisation(
        "Benzene",
        90f,
        out string gas,
        out float vf
      )
    );
    Assert.Equal("BenzeneVapour", gas);
    Assert.Equal(12f, vf);
    Assert.True(
      ExLiquids.Taxonomy.TryCondensation(
        "BenzeneVapour",
        70f,
        out string liq,
        out float cf
      )
    );
    Assert.Equal("Benzene", liq);
    Assert.Equal(12f, cf);
  }
  #endregion

  #region Registry mechanics + JSON binding
  [Fact]
  public void SeedDefaults_registers_the_four_builtins()
  {
    Assert.Equal(4, ExLiquids.All.Count);
    Assert.True(ExLiquids.TryGet("Steam", out var steam));
    Assert.Equal(LiquidPhase.Gas, steam.Phase);
  }

  [Fact]
  public void Clear_empties_the_registry()
  {
    ExLiquids.Clear();
    Assert.Empty(ExLiquids.All);
  }

  [Fact]
  public void LiquidCatalogue_binds_camelCase_json_and_the_phase_enum()
  {
    const string json =
      @"{ ""code"": ""liquid"", ""liquids"": [
          { ""code"": ""Oil"", ""phase"": ""liquid"", ""priority"": 0,
            ""vaporisesTo"": ""OilVapour"", ""boilPointC"": 300, ""vaporiseVolumeFactor"": 8 },
          { ""code"": ""Steam"", ""phase"": ""gas"", ""priority"": 10,
            ""condensesTo"": ""Water"", ""condenseBelowC"": 100 } ] }";

    var cat = JsonConvert.DeserializeObject<LiquidCatalogue>(json)!;

    Assert.NotNull(cat.Liquids);
    Assert.Equal(2, cat.Liquids!.Count);
    Assert.Equal("Oil", cat.Liquids[0].Code);
    Assert.Equal(LiquidPhase.Liquid, cat.Liquids[0].Phase); // "liquid" → Liquid (case-insensitive)
    Assert.Equal("OilVapour", cat.Liquids[0].VaporisesTo);
    Assert.Equal(300f, cat.Liquids[0].BoilPointC);
    Assert.Equal(8f, cat.Liquids[0].VaporiseVolumeFactor);
    Assert.Equal(LiquidPhase.Gas, cat.Liquids[1].Phase);
    Assert.Equal("Water", cat.Liquids[1].CondensesTo);
    Assert.Equal(100f, cat.Liquids[1].CondenseBelowC);
  }

  [Fact]
  public void The_shipped_liquids_json_reproduces_the_builtin_seed()
  {
    // exlib ships assets/exlib/config/liquids.json as the data-authored baseline (the "author a medium
    // as data" deliverable + a modder template). It re-declares the four built-ins, which SeedDefaults
    // also seeds in code - so the load-bearing guarantee is that the two never drift: the file must bind
    // to exactly the seeded set, field-for-field, or a fresh install stops being byte-identical.
    var seeded = ExLiquids.All.ToDictionary(
      d => d.Code,
      StringComparer.OrdinalIgnoreCase
    );

    string path = Path.Combine(
      RepoRoot(),
      "assets",
      "exlib",
      "config",
      "liquids.json"
    );
    Assert.True(File.Exists(path), $"shipped liquids.json missing at {path}");

    var cat = JsonConvert.DeserializeObject<LiquidCatalogue>(
      File.ReadAllText(path)
    )!;
    Assert.NotNull(cat.Liquids);

    Assert.Equal(
      seeded.Keys.OrderBy(k => k, StringComparer.Ordinal),
      cat.Liquids!.Select(d => d.Code).OrderBy(k => k, StringComparer.Ordinal)
    );

    foreach (LiquidDef fileDef in cat.Liquids!)
    {
      Assert.True(
        seeded.TryGetValue(fileDef.Code, out LiquidDef seed),
        $"shipped liquid '{fileDef.Code}' is not one of the seeded built-ins"
      );
      Assert.Equal(seed.Phase, fileDef.Phase);
      Assert.Equal(seed.Priority, fileDef.Priority);
      Assert.Equal(seed.CondensesTo, fileDef.CondensesTo);
      Assert.Equal(seed.CondenseBelowC, fileDef.CondenseBelowC);
      Assert.Equal(seed.CondenseVolumeFactor, fileDef.CondenseVolumeFactor);
      Assert.Equal(seed.VaporisesTo, fileDef.VaporisesTo);
      Assert.Equal(seed.BoilPointC, fileDef.BoilPointC);
      Assert.Equal(seed.VaporiseVolumeFactor, fileDef.VaporiseVolumeFactor);
    }
  }
  #endregion

  private static string RepoRoot()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from "
          + AppContext.BaseDirectory
      );
  }
}
