using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace SteelmakingExpanded;

/// <summary>
/// The steelmaking recipe cost catalogue, written to <c>ModConfig/smex_recipes.json</c> alongside the
/// main <c>smex_values.json</c>. Same shape and behaviour as ppex's catalogue: each entry names a grid recipe
/// (by output) or RCC construction (by block) to manage; the <c>normal</c> level is auto-filled from
/// the recipes as authored, and the <c>cheap</c> level is scale-filled from it (then editable). The
/// active level is chosen by <c>/exmod steel &lt;level&gt;</c> (stored in
/// <see cref="SmexConfig.RecipeLevel"/>) and applied on the next world reload.
/// </summary>
[ExConfigRegister("smex_recipes.json", "smex")]
public class SmexRecipeConfig : IExVersionedConfig
{
  public string? ConfigVersion { get; set; }

  private Dictionary<string, RecipeCostEntry>? _recipes;

  /// <summary>Never null: a missing or null <c>Recipes</c> in the file falls back to the code
  /// defaults. Missing/broken individual entries are repaired against <see cref="DefaultCatalogue"/>
  /// at load by <see cref="ExRecipeCosts.Reconcile"/>.</summary>
  public Dictionary<string, RecipeCostEntry> Recipes
  {
    get => _recipes ??= Defaults();
    set => _recipes = value;
  }

  /// <summary>A fresh copy of the shipped catalogue defaults, used to repair the loaded file.</summary>
  public static Dictionary<string, RecipeCostEntry> DefaultCatalogue() =>
    Defaults();

  private static RecipeCostEntry Grid(string match) =>
    new() { Kind = "grid", Match = match };

  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new()
    {
      // RCC construction (the Bessemer converter vessel).
      ["converterbessemer-rcc"] = new()
      {
        Kind = "rcc",
        Match = "smex:converterbessemer-*",
      },

      // Grid recipes for the steelmaking machines / components.
      ["airblower-grid"] = Grid("smex:engineairblower-*"),
      ["convertercontrol-grid"] = Grid("smex:convertercontrol-*"),
      ["convertertransmission-grid"] = Grid("smex:convertertransmission-*"),
      ["converterintake-grid"] = Grid("smex:converter-intake-*"),
      ["blastfurnacedoor-grid"] = Grid("smex:blastfurnacedoor"),
      ["cowperstove-grid"] = Grid("smex:cowperstove-intake-*"),
      ["smokestack-grid"] = Grid("smex:smokestack-intake-*"),
      ["moltenbarrel-grid"] = Grid("smex:moltenbarrel"),
    };
}
