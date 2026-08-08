using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace HighPressureExpanded;

/// <summary>
/// The high-pressure machine recipe cost catalogue, the <c>hpex</c> section of the shared
/// <c>ModConfig/ex_recipes.json</c>. Each entry names a grid recipe (by output) or an RCC
/// construction (by block) and its ingredient totals per cost level. The <c>normal</c> level is
/// auto-filled from the recipes as authored on first run, and a level a recipe does not pin is
/// filled by scaling <c>normal</c>, so every number lands in the file as an editable value. The
/// active level comes from <c>/exmod recipes hpex &lt;level&gt;</c>, stored in
/// <see cref="HpexConfig.RecipeLevel"/>, and applies on the next world reload.
/// </summary>
[ExConfigRegister("ex_recipes.json", "hpex")]
public class HpexRecipeConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  private Dictionary<string, RecipeCostEntry>? _recipes;

  /// <summary>Never null: a missing or null <c>Recipes</c> in the file falls back to the code
  /// defaults, so a bad edit cannot blank the catalogue. Individual missing or broken entries are
  /// repaired against <see cref="DefaultCatalogue"/> at load by <see cref="ExRecipeCosts.Reconcile"/>.</summary>
  public Dictionary<string, RecipeCostEntry> Recipes {
    get => _recipes ??= Defaults();
    set => _recipes = value;
  }

  /// <summary>A fresh copy of the shipped catalogue defaults, used to repair the loaded file.</summary>
  public static Dictionary<string, RecipeCostEntry> DefaultCatalogue() =>
    Defaults();

  private static RecipeCostEntry Rcc(string match) =>
    new() { Type = "rcc", Match = match };

  private static RecipeCostEntry Grid(string match) =>
    new() { Type = "grid", Match = match };

  // Every grid and RCC recipe this mod ships, kept in sync by hand. Profiles are auto-filled at
  // load: the normal baseline is read from the live recipe, the cheap profile is scaled to half
  // cost, and both are editable in the file.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new() {
      // RCC constructions (the heavy multiblock build costs).
      ["boilerlancashire-rcc"] = Rcc("hpex:boilerlancashire-*"),
      ["enginecornish-rcc"] = Rcc("hpex:enginecornish-*"),

      // Machine grid "frame" recipes.
      ["boilerlancashire-grid"] = Grid("hpex:boilerlancashire-*"),
      ["enginecornish-grid"] = Grid("hpex:enginecornish-*"),
    };
}
