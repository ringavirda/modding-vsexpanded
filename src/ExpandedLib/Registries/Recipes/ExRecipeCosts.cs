using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ExpandedLib.Registries.Recipes;

/// <summary>
/// Generic recipe-cost adjuster: rewrites the ingredient quantities of grid (crafting) recipes and
/// right-click-construction (RCC) blocks to a named "cost level" from a catalogue, so a mod can offer
/// a balance toggle (e.g. <c>cheap</c> vs <c>normal</c>). Catalogue keys are wildcard-aware block /
/// output codes, so a whole variant family is matched at once.
/// <para>
/// Apply at load, once recipes have resolved (a mod system's <c>StartServerSide</c>/
/// <c>StartClientSide</c>), on the side that owns the recipe. Grid recipes are edited in the live
/// <c>GridRecipes</c> list; an RCC block's stages are rewritten on its behaviour-properties JSON,
/// which the vanilla behaviour re-parses for every construction - so the new cost applies to
/// constructions started after a world reload, not ones already in progress.
/// </para>
/// </summary>
public static class ExRecipeCosts
{
  /// <summary>The level whose numbers mirror the recipes as authored; auto-filled from the live
  /// recipe so a mod ships only its alternate level(s).</summary>
  public const string LevelNormal = "normal";

  private const string RccBehaviorName = "RightClickConstructable";

  /// <summary>
  /// Fills the <see cref="LevelNormal"/> level of every catalogue entry that lacks it by reading the
  /// live recipe's current quantities. Returns <c>true</c> if anything was added, so the caller can
  /// persist the catalogue. Run this BEFORE <see cref="Apply"/> (which mutates the live recipes).
  /// </summary>
  public static bool EnsureNormalExtracted(
    ICoreAPI api,
    IDictionary<string, RecipeCostEntry> catalogue
  )
  {
    bool changed = false;
    foreach (var (key, entry) in catalogue)
    {
      if (
        string.IsNullOrEmpty(entry.Match)
        || (
          entry.Levels.TryGetValue(LevelNormal, out var existing)
          && existing.Count > 0
        )
      )
        continue;

      var normal = IsRcc(entry)
        ? ReadRcc(api, new AssetLocation(entry.Match))
        : ReadGrid(api, new AssetLocation(entry.Match));
      if (normal.Count > 0)
      {
        entry.Levels[LevelNormal] = normal;
        changed = true;
      }
    }
    return changed;
  }

  /// <summary>
  /// Fills <paramref name="level"/> for any entry that lacks it by scaling its <see cref="LevelNormal"/>
  /// quantities by <paramref name="factor"/> (each floored at 1). Lets a mod ship a whole alternate
  /// level from one number while still letting a recipe pin exact values by pre-seeding that level.
  /// Run after <see cref="EnsureNormalExtracted"/>. Returns <c>true</c> if anything was added.
  /// </summary>
  public static bool EnsureScaledLevel(
    IDictionary<string, RecipeCostEntry> catalogue,
    string level,
    double factor
  )
  {
    bool changed = false;
    foreach (var entry in catalogue.Values)
    {
      if (
        (
          entry.Levels.TryGetValue(level, out var existing)
          && existing.Count > 0
        ) || !entry.Levels.TryGetValue(LevelNormal, out var normal)
      )
        continue;

      entry.Levels[level] = normal.ToDictionary(
        kv => kv.Key,
        kv => Math.Max(1, (int)Math.Round(kv.Value * factor))
      );
      changed = true;
    }
    return changed;
  }

  /// <summary>
  /// Repairs a player-edited catalogue against the mod's code <paramref name="defaults"/> so a wrong
  /// edit or deletion can't break recipe loading: restores any deleted entry, fixes the structural
  /// fields (<see cref="RecipeCostEntry.Match"/>/<see cref="RecipeCostEntry.Kind"/>, which aren't meant
  /// to be edited) of present entries, restores any pinned default level a player removed, and clamps
  /// every quantity to at least 1. Player-set level numbers (≥1) and extra player-added entries are
  /// kept. Run before <see cref="EnsureNormalExtracted"/>/<see cref="Apply"/>. Returns <c>true</c> if
  /// anything was changed (so the caller can persist the repaired file).
  /// </summary>
  public static bool Reconcile(
    IDictionary<string, RecipeCostEntry> live,
    IReadOnlyDictionary<string, RecipeCostEntry> defaults
  )
  {
    bool changed = false;

    foreach (var (key, def) in defaults)
    {
      if (!live.TryGetValue(key, out var cur) || cur == null)
      {
        live[key] = def; // a fresh defaults instance, safe to adopt wholesale
        changed = true;
        continue;
      }

      if (cur.Match != def.Match)
      {
        cur.Match = def.Match;
        changed = true;
      }
      if (cur.Kind != def.Kind)
      {
        cur.Kind = def.Kind;
        changed = true;
      }
      cur.Levels ??= new();

      // Restore any non-empty default level the player deleted (e.g. a pinned "cheap").
      foreach (var (levelName, defNums) in def.Levels)
        if (
          defNums.Count > 0
          && (
            !cur.Levels.TryGetValue(levelName, out var curNums)
            || curNums.Count == 0
          )
        )
        {
          cur.Levels[levelName] = new Dictionary<string, int>(defNums);
          changed = true;
        }
    }

    // Clamp every quantity (in default and player-added entries alike) to a safe minimum.
    foreach (var entry in live.Values)
    {
      if (entry?.Levels == null)
        continue;
      foreach (var level in entry.Levels.Values)
      foreach (var ingredient in level.Keys.ToList())
        if (level[ingredient] < 1)
        {
          level[ingredient] = 1;
          changed = true;
        }
    }

    return changed;
  }

  /// <summary>Applies the named cost <paramref name="level"/> to every recipe in the catalogue. A
  /// missing or empty level for an entry leaves that recipe untouched.</summary>
  public static void Apply(
    ICoreAPI api,
    IReadOnlyDictionary<string, RecipeCostEntry> catalogue,
    string level
  )
  {
    foreach (var entry in catalogue.Values)
    {
      if (
        string.IsNullOrEmpty(entry.Match)
        || !entry.Levels.TryGetValue(level, out var costs)
        || costs.Count == 0
      )
        continue;

      var code = new AssetLocation(entry.Match);
      if (IsRcc(entry))
        ApplyRcc(api, code, costs);
      else
        ApplyGrid(api, code, costs);
    }
  }

  private static bool IsRcc(RecipeCostEntry entry) =>
    string.Equals(entry.Kind, "rcc", StringComparison.OrdinalIgnoreCase);

  #region Grid recipes

  private static IEnumerable<GridRecipe> GridRecipesFor(
    ICoreAPI api,
    AssetLocation outputWildcard
  ) =>
    api.World.GridRecipes.Where(r =>
      r.Output?.Code is { } c && WildcardUtil.Match(outputWildcard, c)
    );

  private static Dictionary<string, int> ReadGrid(
    ICoreAPI api,
    AssetLocation output
  )
  {
    var map = new Dictionary<string, int>();

    foreach (var recipe in GridRecipesFor(api, output))
    {
      if (recipe.Ingredients != null)
        foreach (var ing in recipe.Ingredients.Values)
          if (ing?.Code != null && !ing.IsTool)
            map[ing.Code.ToString()] = ing.Quantity;

      if (recipe.ResolvedIngredients != null)
        foreach (var ing in recipe.ResolvedIngredients)
          if (ing?.Code != null && !ing.IsTool)
            map[ing.Code.ToString()] = ing.Quantity;
    }

    return map;
  }

  private static void ApplyGrid(
    ICoreAPI api,
    AssetLocation output,
    IReadOnlyDictionary<string, int> costs
  )
  {
    foreach (var recipe in GridRecipesFor(api, output))
    {
      SetGridIngredients(recipe.ResolvedIngredients, costs);
      if (recipe.Ingredients != null)
        SetGridIngredients(recipe.Ingredients.Values, costs);
    }
  }

  private static void SetGridIngredients(
    IEnumerable<CraftingRecipeIngredient?>? ingredients,
    IReadOnlyDictionary<string, int> costs
  )
  {
    if (ingredients == null)
      return;

    foreach (var ing in ingredients)
    {
      if (
        ing?.Code != null
        && costs.TryGetValue(ing.Code.ToString(), out int q)
      )
      {
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

  private static Dictionary<string, int> ReadRcc(
    ICoreAPI api,
    AssetLocation blockCode
  )
  {
    var map = new Dictionary<string, int>();
    var block = RccBlocksFor(api, blockCode).FirstOrDefault();
    if (block == null)
      return map;

    foreach (var token in RccQuantityTokens(RccStages(block)!))
    {
      map[token.Key] = map.TryGetValue(token.Key, out int prev)
        ? prev + token.Value.Value<int>()
        : token.Value.Value<int>();
    }
    return map;
  }

  private static void ApplyRcc(
    ICoreAPI api,
    AssetLocation blockCode,
    IReadOnlyDictionary<string, int> costs
  )
  {
    // Each variant block carries its own properties JSON, so rewrite them all.
    foreach (var block in RccBlocksFor(api, blockCode))
    {
      var byKey = new Dictionary<string, List<JToken>>();
      foreach (var (key, token) in RccQuantityTokens(RccStages(block)!))
        (byKey.TryGetValue(key, out var list) ? list : byKey[key] = new()).Add(
          token
        );

      foreach (var (key, total) in costs)
        if (byKey.TryGetValue(key, out var tokens))
          Distribute(tokens, total);
    }
  }

  /// <summary>Yields (ingredient key, quantity JToken) for every require-stack across the stages, in
  /// stage order. The JToken is the live quantity value, so callers can read or overwrite it.</summary>
  private static IEnumerable<KeyValuePair<string, JToken>> RccQuantityTokens(
    JArray stages
  )
  {
    foreach (var stage in stages)
    {
      if (stage["requireStacks"] is not JArray reqs)
        continue;
      foreach (var req in reqs)
      {
        string? key =
          req["name"]?.Value<string>() ?? req["code"]?.Value<string>();
        var qty = req["quantity"];
        if (key != null && qty != null)
          yield return new KeyValuePair<string, JToken>(key, qty);
      }
    }
  }

  /// <summary>
  /// Distributes <paramref name="total"/> across the per-stage quantity <paramref name="tokens"/>
  /// proportionally to their current values (remainder on the last), so the staged structure is kept
  /// and the quantities still sum to exactly the requested total.
  /// </summary>
  private static void Distribute(List<JToken> tokens, int total)
  {
    total = Math.Max(0, total);
    int current = tokens.Sum(t => t.Value<int>());

    if (current <= 0)
    {
      // No proportions to follow - spread as evenly as possible.
      for (int i = 0; i < tokens.Count; i++)
        Set(
          tokens[i],
          total / tokens.Count + (i < total % tokens.Count ? 1 : 0)
        );
      return;
    }

    int assigned = 0;
    for (int i = 0; i < tokens.Count; i++)
    {
      int q =
        i == tokens.Count - 1
          ? total - assigned
          : (int)Math.Round((double)tokens[i].Value<int>() * total / current);
      q = Math.Max(0, q);
      assigned += q;
      Set(tokens[i], q);
    }
  }

  private static void Set(JToken quantityToken, int value) =>
    ((JValue)quantityToken).Value = (long)value;

  #endregion
}
