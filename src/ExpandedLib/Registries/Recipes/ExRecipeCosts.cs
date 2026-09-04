using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Registries.Recipes;

/// <summary>
/// Rewrites the ingredient quantities and grid output count of grid crafting recipes and
/// right-click-construction (RCC) blocks to a named cost profile from a catalogue, giving a mod a balance
/// toggle such as <c>cheap</c> against <c>normal</c>. Catalogue keys are wildcard-aware block or output
/// codes, matching a whole variant family at once. Apply at load once recipes have resolved
/// (<c>StartServerSide</c> or <c>StartClientSide</c>), on the side that owns the recipe. RCC stages are
/// rewritten on the block's behaviour-properties JSON, which the vanilla behaviour re-parses per
/// construction, so a new cost reaches only constructions started after a world reload.
/// </summary>
public static class ExRecipeCosts {
  /// <summary>The profile whose numbers mirror the recipes as authored. Filled from the live recipe, so
  /// a mod ships only its alternate profiles.</summary>
  public const string ProfileNormal = "normal";

  private const string RccBehaviorName = "ExRightClickConstructable";

  /// <summary>
  /// Fills the <see cref="ProfileNormal"/> profile of every catalogue entry that lacks it from the live
  /// recipe's current quantities. Must run before <see cref="Apply"/>, which mutates those recipes.
  /// Returns <c>true</c> if anything was added, so the caller can persist the catalogue.
  /// </summary>
  public static bool EnsureNormalExtracted(
    ICoreAPI api,
    IDictionary<string, RecipeCostEntry> catalogue
  ) {
    bool changed = false;
    foreach (var entry in catalogue.Values) {
      if (
        string.IsNullOrEmpty(entry.Match)
        || (
          entry.Profiles.TryGetValue(ProfileNormal, out var existing)
          && existing.HasContent
        )
      )
        continue;

      var normal = IsRcc(entry)
        ? ReadRcc(api, new AssetLocation(entry.Match))
        : ReadGrid(api, new AssetLocation(entry.Match));
      if (normal.HasContent) {
        entry.Profiles[ProfileNormal] = normal;
        changed = true;
      }
    }
    return changed;
  }

  /// <summary>
  /// Fills <paramref name="profile"/> for entries lacking costs by scaling their
  /// <see cref="ProfileNormal"/> quantities by <paramref name="factor"/>, each floored at 1; a value already
  /// pinned in the profile is kept. Run after <see cref="EnsureNormalExtracted"/>. Returns <c>true</c> if
  /// anything changed.
  /// </summary>
  public static bool EnsureScaledLevel(
    IDictionary<string, RecipeCostEntry> catalogue,
    string profile,
    double factor
  ) {
    bool changed = false;
    foreach (var entry in catalogue.Values) {
      if (!entry.Profiles.TryGetValue(ProfileNormal, out var normal))
        continue;

      entry.Profiles.TryGetValue(profile, out var target);
      // Cost data (ingredients/stages) already filled: leave it and any pin alone.
      bool hasCosts =
        target != null
        && (
          (target.Ingredients is { Count: > 0 })
          || (target.Stages is { Count: > 0 })
        );
      if (hasCosts)
        continue;

      target ??= new RecipeProfileCost(); // keep any pinned Quantity already here
      if (normal.Ingredients is { Count: > 0 })
        target.Ingredients = normal.Ingredients.ToDictionary(
          kv => kv.Key,
          kv => Scale(kv.Value, factor)
        );
      if (normal.Stages is { Count: > 0 })
        target.Stages = normal.Stages.ToDictionary(
          s => s.Key,
          s => s.Value.ToDictionary(kv => kv.Key, kv => Scale(kv.Value, factor))
        );

      entry.Profiles[profile] = target;
      changed = true;
    }
    return changed;
  }

  /// <summary>
  /// Repairs an edited catalogue against the code <paramref name="defaults"/>: restores deleted entries and
  /// removed pins, resets <see cref="RecipeCostEntry.Match"/> and <see cref="RecipeCostEntry.Type"/>, and
  /// clamps every quantity to at least 1. Edited numbers of 1 or more and added entries are kept. Run
  /// before <see cref="EnsureNormalExtracted"/> and <see cref="Apply"/>; <c>true</c> if anything changed.
  /// </summary>
  public static bool Reconcile(
    IDictionary<string, RecipeCostEntry> live,
    IReadOnlyDictionary<string, RecipeCostEntry> defaults
  ) {
    bool changed = false;

    foreach (var (key, def) in defaults) {
      if (!live.TryGetValue(key, out var cur) || cur == null) {
        live[key] = def; // fresh defaults instance, safe to adopt wholesale
        changed = true;
        continue;
      }

      if (cur.Type != def.Type) {
        cur.Type = def.Type;
        changed = true;
      }
      if (cur.Match != def.Match) {
        cur.Match = def.Match;
        changed = true;
      }
      cur.Profiles ??= new();

      foreach (var (name, defProfile) in def.Profiles) {
        if (!defProfile.HasContent)
          continue;

        if (
          !cur.Profiles.TryGetValue(name, out var curProfile)
          || curProfile == null
        ) {
          // A pinned default profile was removed in full.
          cur.Profiles[name] = defProfile;
          changed = true;
        } else if (defProfile.Quantity.HasValue && !curProfile.Quantity.HasValue) {
          // Only the pinned output quantity was removed.
          curProfile.Quantity = defProfile.Quantity;
          changed = true;
        }
      }
    }

    // Clamp every quantity, in default and added entries alike, to a safe minimum.
    foreach (var entry in live.Values) {
      if (entry?.Profiles == null)
        continue;
      foreach (var profile in entry.Profiles.Values)
        changed |= ClampProfile(profile);
    }

    return changed;
  }

  private static bool ClampProfile(RecipeProfileCost profile) {
    bool changed = false;
    if (profile.Ingredients != null)
      foreach (var k in profile.Ingredients.Keys.ToList())
        if (profile.Ingredients[k] < 1) {
          profile.Ingredients[k] = 1;
          changed = true;
        }
    if (profile.Stages != null)
      foreach (var stage in profile.Stages.Values)
        foreach (var k in stage.Keys.ToList())
          if (stage[k] < 1) {
            stage[k] = 1;
            changed = true;
          }
    if (profile.Quantity is < 1) {
      profile.Quantity = 1;
      changed = true;
    }
    return changed;
  }

  /// <summary>Applies the named cost <paramref name="profile"/> to every recipe in the catalogue. A
  /// missing or empty profile for an entry leaves that recipe untouched.</summary>
  public static void Apply(
    ICoreAPI api,
    IDictionary<string, RecipeCostEntry> catalogue,
    string profile
  ) {
    foreach (var entry in catalogue.Values) {
      if (
        string.IsNullOrEmpty(entry.Match)
        || !entry.Profiles.TryGetValue(profile, out var costs)
        || costs == null
      )
        continue;

      var code = new AssetLocation(entry.Match);
      if (IsRcc(entry))
        ApplyRcc(api, code, costs.Stages);
      else
        ApplyGrid(api, code, costs.Ingredients, costs.Quantity);
    }
  }

  private static int Scale(int value, double factor) =>
    Math.Max(1, (int)Math.Round(value * factor));

  private static bool IsRcc(RecipeCostEntry entry) =>
    string.Equals(entry.Type, "rcc", StringComparison.OrdinalIgnoreCase);

  #region Grid recipes

  private static IEnumerable<GridRecipe> GridRecipesFor(
    ICoreAPI api,
    AssetLocation outputWildcard
  ) =>
    api.World.GridRecipes.Where(r =>
      r.Output?.Code is { } c && WildcardUtil.Match(outputWildcard, c)
    );

  private static RecipeProfileCost ReadGrid(ICoreAPI api, AssetLocation output) {
    var map = new Dictionary<string, int>();

    foreach (var recipe in GridRecipesFor(api, output)) {
      if (recipe.Ingredients != null)
        foreach (var ing in recipe.Ingredients.Values)
          if (ing?.Code != null && !ing.IsTool)
            map[ing.Code.ToString()] = ing.Quantity;

      if (recipe.ResolvedIngredients != null)
        foreach (var ing in recipe.ResolvedIngredients)
          if (ing?.Code != null && !ing.IsTool)
            map[ing.Code.ToString()] = ing.Quantity;
    }

    return new RecipeProfileCost { Ingredients = map };
  }

  private static void ApplyGrid(
    ICoreAPI api,
    AssetLocation output,
    IReadOnlyDictionary<string, int>? ingredients,
    int? quantity
  ) {
    bool hasIngredients = ingredients is { Count: > 0 };
    if (!hasIngredients && quantity is not > 0)
      return;

    foreach (var recipe in GridRecipesFor(api, output)) {
      if (hasIngredients) {
        SetGridIngredients(recipe.ResolvedIngredients, ingredients!);
        if (recipe.Ingredients != null)
          SetGridIngredients(recipe.Ingredients.Values, ingredients!);
      }
      if (quantity is > 0)
        SetGridOutput(recipe, quantity.Value);
    }
  }

  /// <summary>Sets a grid recipe's crafted output count: both <c>Quantity</c> and the resolved stack's
  /// <c>StackSize</c>, the latter being what ends up in the output slot.</summary>
  private static void SetGridOutput(GridRecipe recipe, int qty) {
    if (recipe.Output == null)
      return;
    recipe.Output.Quantity = qty;
    if (recipe.Output.ResolvedItemStack != null)
      recipe.Output.ResolvedItemStack.StackSize = qty;
  }

  private static void SetGridIngredients(
    IEnumerable<CraftingRecipeIngredient?>? ingredients,
    IReadOnlyDictionary<string, int> costs
  ) {
    if (ingredients == null)
      return;

    foreach (var ing in ingredients) {
      if (
        ing?.Code != null
        && costs.TryGetValue(ing.Code.ToString(), out int q)
      ) {
        ing.Quantity = q;
        ing.ResolvedItemStack?.StackSize = q;
      }
    }
  }

  #endregion

  #region RCC blocks

  private static IEnumerable<Block> RccBlocksFor(
    ICoreAPI api,
    AssetLocation blockWildcard
  ) =>
    api.World.Blocks.Where(b =>
      b?.Code is { } c
      && WildcardUtil.Match(blockWildcard, c)
      && RccStages(b) != null
    );

  private static JArray? RccStages(Block block) =>
    block
      .BlockEntityBehaviors?.FirstOrDefault(b => b.Name == RccBehaviorName)
      ?.properties?.Token?["stages"] as JArray;

  private static RecipeProfileCost ReadRcc(
    ICoreAPI api,
    AssetLocation blockCode
  ) {
    var stages = new Dictionary<string, Dictionary<string, int>>();
    var block = RccBlocksFor(api, blockCode).FirstOrDefault();
    if (block == null)
      return new RecipeProfileCost { Stages = stages };

    var jstages = RccStages(block)!;
    for (int i = 0; i < jstages.Count; i++) {
      if (jstages[i]["requireStacks"] is not JArray reqs)
        continue;
      var map = new Dictionary<string, int>();
      foreach (var req in reqs) {
        string? name =
          req["name"]?.Value<string>() ?? req["code"]?.Value<string>();
        var qty = req["quantity"];
        if (name != null && qty != null)
          map[name] = qty.Value<int>();
      }
      if (map.Count > 0)
        stages[i.ToString()] = map;
    }
    return new RecipeProfileCost { Stages = stages };
  }

  private static void ApplyRcc(
    ICoreAPI api,
    AssetLocation blockCode,
    IReadOnlyDictionary<string, Dictionary<string, int>>? stages
  ) {
    if (stages is not { Count: > 0 })
      return;

    // Each variant block carries its own properties JSON, so all of them are rewritten. Each per-stage
    // ingredient is set directly from its own catalogue entry, with no redistribution.
    foreach (var block in RccBlocksFor(api, blockCode)) {
      var jstages = RccStages(block)!;
      foreach (var (stageKey, names) in stages) {
        if (
          !int.TryParse(stageKey, out int i)
          || i < 0
          || i >= jstages.Count
          || jstages[i]["requireStacks"] is not JArray reqs
        )
          continue;

        foreach (var req in reqs) {
          string? name =
            req["name"]?.Value<string>() ?? req["code"]?.Value<string>();
          if (
            name != null
            && req["quantity"] is { } qty
            && names.TryGetValue(name, out int q)
          )
            Set(qty, q);
        }
      }
    }
  }

  private static void Set(JToken quantityToken, int value) =>
    ((JValue)quantityToken).Value = (long)value;

  #endregion
}
