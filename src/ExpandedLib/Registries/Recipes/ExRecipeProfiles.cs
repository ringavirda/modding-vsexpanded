using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Recipes;

/// <summary>
/// Process-wide registry of <see cref="RecipeProfile"/>s keyed by mod code, for mods that expose
/// recipe-cost levels (<c>normal</c>, <c>cheap</c>, ...). It owns the apply pipeline every mod shares
/// (repair, discover, fill levels, persist, apply); a dependent mod registers its catalogue and the
/// generic <c>/exmod recipes &lt;code&gt; &lt;level&gt;</c> command plus exlib's load-time apply drive it.
/// </summary>
public static class ExRecipeProfiles {
  private static readonly ExKeyedRegistry<RecipeProfile> _profiles = new(p =>
    p.Code
  );

  /// <summary>Registers (or replaces) a mod's profile. Call once from the mod's <c>Start</c>, after its
  /// config/catalogue stores have loaded.</summary>
  public static void Register(RecipeProfile profile) =>
    _profiles.Register(profile);

  /// <summary>Looks up a registered profile by mod code (case-insensitive).</summary>
  public static bool TryGet(string code, out RecipeProfile profile) =>
    _profiles.TryGet(code, out profile);

  /// <summary>The registered mod codes, for listing in the command.</summary>
  public static IReadOnlyCollection<string> Codes => _profiles.Codes;

  /// <summary>Runs the apply pipeline for every registered profile. Called from exlib's
  /// <c>StartServerSide</c>/<c>StartClientSide</c>, after every mod has registered in its
  /// <c>Start</c>, so the active level reaches the live recipes on each world load.</summary>
  public static void ApplyAll(ICoreAPI api) {
    foreach (var profile in _profiles.Values)
      Apply(api, profile);
  }

  /// <summary>
  /// Runs the pipeline for one profile: repair the catalogue against the mod's defaults, fill the
  /// <c>normal</c> baseline from the live recipes and the derived levels by scaling it, persist if
  /// anything changed (server only), then apply the selected level to the live grid/RCC recipes.
  /// </summary>
  public static void Apply(ICoreAPI api, RecipeProfile profile) {
    var live = profile.Catalogue();

    bool changed = ExRecipeCosts.Reconcile(live, profile.Defaults());
    changed |= ExRecipeCosts.EnsureNormalExtracted(api, live);
    foreach (var (level, factor) in profile.DerivedLevels)
      changed |= ExRecipeCosts.EnsureScaledLevel(live, level, factor);

    // The catalogue is server-authoritative; the client re-derives in memory for its handbook but
    // must not write the shared single-player file (it would clobber the server's grid entries).
    if (changed && api.Side == EnumAppSide.Server)
      profile.SaveCatalogue();

    ExRecipeCosts.Apply(api, live, profile.GetLevel());
  }
}
