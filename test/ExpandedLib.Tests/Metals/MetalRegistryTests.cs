using ExpandedLib;
using ExpandedLib.Metals;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The process-wide metal catalogue. The load-bearing guarantee is that an <b>unregistered</b> metal -
/// i.e. every metal until the asset loader lands, and any metal that never ships a <see cref="MetalDef"/> -
/// reads back the exact conventions that used to live inline in <c>MoltenMetal</c> / <c>MoltenChisel</c>,
/// so the registry is a pure read-only overlay with no behaviour change. The registered-override branch is
/// pinned alongside it. The registry is a static store, so each test clears it first for isolation.
/// </summary>
[Collection("MetalRegistry")] // the registry is a process-wide static; serialize the mutating classes
public class MetalRegistryTests
{
  public MetalRegistryTests() => MetalRegistry.Clear();

  #region Convention branch (unregistered = today's behaviour)
  [Theory]
  [InlineData("game:ingot-iron", "game:metalbit-iron")] // ingot-X → metalbit-X, same domain
  [InlineData("game:ingot-steel", "game:metalbit-steel")]
  [InlineData("iwex:slag", "iwex:slag")] // non-ingot carrier drops as itself
  public void SolidDropOf_reproduces_the_shatteredStack_convention(
    string molten,
    string expected
  )
  {
    Assert.Equal(
      expected,
      MetalRegistry.SolidDropOf(new AssetLocation(molten)).ToString()
    );
  }

  [Theory]
  [InlineData("game:ingot-iron", "Iron")] // strip ingot-, capitalise
  [InlineData("iwex:slag", "Slag")] // non-ingot used verbatim, capitalised
  [InlineData("game:metalbit-copper", "Metalbit-copper")]
  public void DisplayName_reproduces_the_strip_and_capitalise_convention(
    string molten,
    string expected
  )
  {
    Assert.Equal(expected, MetalRegistry.DisplayName(molten));
  }

  [Fact]
  public void DisplayName_of_empty_code_is_the_unknown_label()
  {
    // Lang.Get echoes the key headless - the point is the empty-code branch, not the translation.
    Assert.Equal("exlib:metal-unknown", MetalRegistry.DisplayName(""));
  }

  [Fact]
  public void UnitsPerBit_defaults_to_five()
  {
    Assert.Equal(5, MetalRegistry.UnitsPerBitOf(new AssetLocation("game:ingot-iron")));
  }

  [Fact]
  public void Thresholds_default_to_the_global_exlib_values()
  {
    var iron = new AssetLocation("game:ingot-iron");
    Assert.Equal(ExlibValues.MetalLiquidThreshold, MetalRegistry.LiquidThresholdOf(iron));
    Assert.Equal(
      ExlibValues.MetalHardenedThreshold,
      MetalRegistry.HardenedThresholdOf(iron)
    );
    Assert.Equal(ExlibValues.MetalGlowMinTemp, MetalRegistry.GlowMinTempOf(iron));
  }

  [Fact]
  public void FallbackOf_defaults_to_the_global_recovery_fallback()
  {
    Assert.Equal(
      ExlibValues.MetalRecoveryFallback,
      MetalRegistry.FallbackOf(new AssetLocation("game:ingot-iron")).ToString()
    );
  }

  [Fact]
  public void MediaOf_defaults_to_molten()
  {
    Assert.Equal(
      new[] { "molten" },
      MetalRegistry.MediaOf(new AssetLocation("game:ingot-iron"))
    );
  }

  [Theory]
  [InlineData("iron", "game:ingot-iron")] // converter/blast-furnace token → game ingot
  [InlineData("steel", "game:ingot-steel")]
  public void ResolveByCode_of_an_unknown_token_builds_the_game_ingot_code(
    string token,
    string expected
  )
  {
    Assert.Equal(expected, MetalRegistry.MoltenItemOf(token).ToString());
  }
  #endregion

  #region Registered override branch
  [Fact]
  public void A_registered_def_overrides_every_convention()
  {
    MetalRegistry.Register(
      new MetalDef
      {
        Code = "slag",
        MoltenItem = "iwex:slag",
        SolidDrop = "iwex:slagbit",
        UnitsPerBit = 3,
        DisplayLangKey = "iwex:material-slag",
        LiquidThreshold = 0.9f,
        HardenedThreshold = 0.2f,
        GlowMinTemp = 520f,
        RecoveryFallback = "game:ingot-iron",
        Media = new() { "molten", "slurry" },
      }
    );

    var slag = new AssetLocation("iwex:slag");
    Assert.Equal("iwex:slagbit", MetalRegistry.SolidDropOf(slag).ToString());
    Assert.Equal(3, MetalRegistry.UnitsPerBitOf(slag));
    Assert.Equal("iwex:material-slag", MetalRegistry.DisplayName("iwex:slag"));
    Assert.Equal(0.9f, MetalRegistry.LiquidThresholdOf(slag));
    Assert.Equal(0.2f, MetalRegistry.HardenedThresholdOf(slag));
    Assert.Equal(520f, MetalRegistry.GlowMinTempOf(slag));
    Assert.Equal("game:ingot-iron", MetalRegistry.FallbackOf(slag).ToString());
    Assert.Equal(new[] { "molten", "slurry" }, MetalRegistry.MediaOf(slag));
  }

  [Fact]
  public void Classify_reads_a_registered_metals_thresholds_over_the_global_default()
  {
    // A metal whose liquid threshold is 0.9 (vs the global 0.8) and hardened threshold 0.5 (vs 0.3):
    // at the same temperature + melting point it classifies differently from an unregistered metal,
    // proving MoltenMetal.Classify consults the per-metal MetalDef, not just the ExlibValues default.
    MetalRegistry.Register(
      new MetalDef
      {
        Code = "slag",
        MoltenItem = "iwex:slag",
        LiquidThreshold = 0.9f,
        HardenedThreshold = 0.5f,
      }
    );
    var slag = new AssetLocation("iwex:slag");
    var iron = new AssetLocation("game:ingot-iron"); // unregistered → global 0.8 / 0.3
    const float meltPoint = 1000f;

    // 850 °C: above iron's 0.8×1000 liquid line, below slag's 0.9×1000 → iron flows, slag is cooling.
    Assert.Equal(MoltenState.Liquid, MoltenMetal.Classify(850f, meltPoint, iron));
    Assert.Equal(MoltenState.Cooling, MoltenMetal.Classify(850f, meltPoint, slag));

    // 400 °C: above iron's 0.3×1000 hardened line, below slag's 0.5×1000 → iron cooling, slag hardened.
    Assert.Equal(MoltenState.Cooling, MoltenMetal.Classify(400f, meltPoint, iron));
    Assert.Equal(MoltenState.Hardened, MoltenMetal.Classify(400f, meltPoint, slag));
  }

  [Fact]
  public void Classify_of_an_unregistered_metal_uses_the_global_thresholds()
  {
    // Belt-and-braces on the fallback: with nothing registered, the strict-liquid boundary matches
    // the historical StateOf semantics (temp == threshold×meltPoint is NOT liquid).
    var iron = new AssetLocation("game:ingot-iron");
    float liquid = ExlibValues.MetalLiquidThreshold;
    float hardened = ExlibValues.MetalHardenedThreshold;

    Assert.Equal(MoltenState.Liquid, MoltenMetal.Classify(liquid * 1000f + 1f, 1000f, iron));
    Assert.Equal(MoltenState.Cooling, MoltenMetal.Classify(liquid * 1000f, 1000f, iron));
    Assert.Equal(MoltenState.Hardened, MoltenMetal.Classify(hardened * 1000f - 1f, 1000f, iron));
  }

  [Fact]
  public void ResolveByCode_of_a_registered_token_returns_its_molten_item()
  {
    MetalRegistry.Register(new MetalDef { Code = "slag", MoltenItem = "iwex:slag" });

    // Convention would have built game:ingot-slag; the registered def redirects to iwex:slag.
    Assert.Equal("iwex:slag", MetalRegistry.MoltenItemOf("slag").ToString());
  }

  [Fact]
  public void A_registered_def_without_optionals_still_falls_to_convention()
  {
    // Only the two required fields set: every reader must still take the convention branch.
    MetalRegistry.Register(new MetalDef { Code = "iron", MoltenItem = "game:ingot-iron" });

    var iron = new AssetLocation("game:ingot-iron");
    Assert.Equal("game:metalbit-iron", MetalRegistry.SolidDropOf(iron).ToString());
    Assert.Equal("Iron", MetalRegistry.DisplayName("game:ingot-iron"));
    Assert.Equal(5, MetalRegistry.UnitsPerBitOf(iron));
    Assert.Equal(ExlibValues.MetalLiquidThreshold, MetalRegistry.LiquidThresholdOf(iron));
  }
  #endregion

  #region Store mechanics
  [Fact]
  public void Lookup_normalises_the_domain()
  {
    MetalRegistry.Register(new MetalDef { Code = "iron", MoltenItem = "game:ingot-iron" });

    // "ingot-iron" with no explicit domain must resolve to the same game-domain entry.
    Assert.True(MetalRegistry.TryGet("ingot-iron", out var def));
    Assert.Equal("iron", def.Code);
  }

  [Fact]
  public void Clear_empties_the_registry()
  {
    MetalRegistry.Register(new MetalDef { Code = "iron", MoltenItem = "game:ingot-iron" });
    Assert.Single(MetalRegistry.All);

    MetalRegistry.Clear();
    Assert.Empty(MetalRegistry.All);
  }
  #endregion
}
