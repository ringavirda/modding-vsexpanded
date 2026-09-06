using System.Collections.Generic;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="ExRecipeProfiles"/>'s registry and apply pipeline, and <see cref="RecipeProfile"/>'s
/// level switch: registration by code, listing and lookup, and <see cref="ExRecipeProfiles.ApplyAll"/>
/// running the derive/persist/apply cycle over a small hand-built catalogue (the same shape
/// <see cref="ExRecipeCostsTests"/> exercises piece by piece).
/// </summary>
public class RecipeProfilesTests {
  // Distinct per test so parallel test classes touching the same process-wide registry never collide
  // on a code, the same idiom ConfigSyncTests uses for ExConfigProfiles.
  private static string FreshCode() => "stubmod-" + System.Guid.NewGuid().ToString("N")[..8];

  private static RecipeProfile Profile(
    string code,
    Dictionary<string, RecipeCostEntry> catalogue,
    out string level,
    out bool saved
  ) {
    string capturedLevel = "normal";
    bool didSave = false;
    var profile = new RecipeProfile {
      Code = code,
      Catalogue = () => catalogue,
      Defaults = () => catalogue,
      GetLevel = () => capturedLevel,
      SetLevel = l => capturedLevel = l,
      SaveCatalogue = () => didSave = true,
    };
    level = capturedLevel;
    saved = didSave;
    return profile;
  }

  #region Registry: Register / Codes / TryGet

  [Fact]
  public void A_registered_profile_is_found_by_its_code() {
    string code = FreshCode();
    var profile = Profile(code, new(), out _, out _);

    ExRecipeProfiles.Register(profile);

    Assert.True(ExRecipeProfiles.TryGet(code, out var found));
    Assert.Same(profile, found);
    Assert.Contains(code, ExRecipeProfiles.Codes);
  }

  [Fact]
  public void An_unregistered_code_is_not_found() {
    Assert.False(ExRecipeProfiles.TryGet(FreshCode(), out _));
  }

  [Fact]
  public void Registering_the_same_code_again_replaces_the_earlier_profile() {
    string code = FreshCode();
    var first = Profile(code, new(), out _, out _);
    var second = Profile(code, new(), out _, out _);

    ExRecipeProfiles.Register(first);
    ExRecipeProfiles.Register(second);

    Assert.True(ExRecipeProfiles.TryGet(code, out var found));
    Assert.Same(second, found);
  }

  #endregion

  #region RecipeProfile level switching

  [Fact]
  public void GetLevel_and_SetLevel_round_trip_through_the_supplied_delegates() {
    var profile = Profile(FreshCode(), new(), out _, out _);

    Assert.Equal("normal", profile.GetLevel());
    profile.SetLevel("cheap");
    Assert.Equal("cheap", profile.GetLevel());
  }

  [Fact]
  public void Levels_and_DerivedLevels_default_to_normal_and_a_half_cost_cheap() {
    var profile = Profile(FreshCode(), new(), out _, out _);

    Assert.Equal(["normal", "cheap"], profile.Levels);
    Assert.Equal(0.5, profile.DerivedLevels["cheap"]);
  }

  #endregion

  #region ApplyAll

  [Fact]
  public void ApplyAll_over_an_empty_catalogue_never_saves() {
    string code = FreshCode();
    var catalogue = new Dictionary<string, RecipeCostEntry>();
    var profile = Profile(code, catalogue, out _, out bool saved);
    ExRecipeProfiles.Register(profile);
    var world = new TestWorld();

    ExRecipeProfiles.ApplyAll(world.Api);

    Assert.False(saved);
  }

  [Fact]
  public void ApplyAll_derives_a_missing_cheap_level_from_an_already_extracted_normal_and_saves() {
    string code = FreshCode();
    var catalogue = new Dictionary<string, RecipeCostEntry> {
      ["widget"] = new() {
        Type = "grid",
        Match = "stub:widget-*",
        // Already carries "normal", so EnsureNormalExtracted has nothing to read off the live game
        // and this test needs no GridRecipes fixture at all.
        Profiles = new() {
          ["normal"] = new RecipeProfileCost {
            Ingredients = new() { ["stub:iron"] = 4 },
          },
        },
      },
    };
    var profile = Profile(code, catalogue, out _, out _);
    ExRecipeProfiles.Register(profile);
    var world = new TestWorld();
    world.Api.World.GridRecipes.Returns(new List<GridRecipe>());

    ExRecipeProfiles.ApplyAll(world.Api);

    var cheap = catalogue["widget"].Profiles["cheap"];
    Assert.Equal(2, cheap.Ingredients!["stub:iron"]); // half of 4
  }

  #endregion
}
