using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace SteelmakingExpanded;

/// <summary>
/// The steelmaking recipe cost catalogue, written to <c>ModConfig/smex_recipes.json</c> alongside the
/// main <c>smex_values.json</c>. Same shape and behaviour as lpex's catalogue: each entry names a grid recipe
/// (by output) or RCC construction (by block) to manage; the <c>normal</c> level is auto-filled from
/// the recipes as authored, and the <c>cheap</c> level is scale-filled from it (then editable). The
/// active level is chosen by <c>/exmod steel &lt;level&gt;</c> (stored in
/// <see cref="SmexConfig.RecipeLevel"/>) and applied on the next world reload.
/// </summary>
[ExConfigRegister(
  "ex_recipes.json",
  "smex",
  LegacyFileNames = new string[] { "smex_recipes.json" }
)]
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
    new() { Type = "grid", Match = match };

  // Curated list of every grid + RCC recipe this mod ships (kept in sync by hand, mirroring the RCC
  // style). Profiles are auto-filled at load: the normal baseline is read from the live recipe and the
  // cheap profile is scaled (half cost), both editable in the file.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new()
    {
      // RCC construction (the Bessemer converter vessel).
      ["converterbessemer-rcc"] = new()
      {
        Type = "rcc",
        Match = "smex:converterbessemer-*",
      },

      // Converter + hot-blast machine grid recipes. The iron-tier content (blast-furnace components,
      // molten transport, slag paths - all iwex: outputs) lives in IwexRecipeConfig, so
      // /exmod recipes smex discounts steelmaking and nothing else.
      ["converter-intake-grid"] = Grid("smex:converter-intake-*"),
      ["convertercontrol-grid"] = Grid("smex:convertercontrol-*"),
      ["convertertransmission-grid"] = Grid("smex:convertertransmission-*"),
      ["cowperstove-intake-grid"] = Grid("smex:cowperstove-intake-*"),
      ["cowperstoveheatsink-grid"] = Grid("smex:cowperstoveheatsink-*"),
      ["engineairblower-grid"] = Grid("smex:engineairblower-*"),
      ["smokestack-intake-grid"] = Grid("smex:smokestack-intake-*"),

      // Hoppers. These came back to smex with the hot blast furnace (the cold furnace in iwex charges
      // through its own tall hopper instead).
      ["hopperbell-grid"] = Grid("smex:hopperbell"),
      ["hopperreinforced-grid"] = Grid("smex:hopperreinforced"),
    };
}
