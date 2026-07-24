using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace IronworkingExpanded;

/// <summary>
/// The ironworking recipe cost catalogue, written to <c>ModConfig/ex_recipes.json</c> alongside the main
/// <c>ex_values.json</c>. Same shape and behaviour as every other mod's catalogue: each entry names a grid
/// recipe (by output) or an RCC construction (by block) to manage; the <c>normal</c> level is auto-filled
/// from the recipes as authored, and the <c>cheap</c> level is scale-filled from it (then editable). The
/// active level is chosen by <c>/exmod recipes iwex &lt;level&gt;</c> (stored in
/// <see cref="IwexConfig.RecipeLevel"/>) and applied on the next world reload.
/// <para>
/// These entries used to sit in <c>SmexRecipeConfig</c> - a leftover from when the molten, blast-furnace
/// and slag-path subsystems lived in smex. Matching an <c>iwex:</c> output from the steel catalogue worked,
/// but it meant the iron tier had no cost switch of its own: a player who wanted cheaper ironmaking had to
/// discount steelmaking with it. Each mod now owns the costs of the content it ships.
/// </para>
/// </summary>
[ExConfigRegister("ex_recipes.json", "iwex")]
public class IwexRecipeConfig : IExVersionedConfig
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

  // Curated list of every grid recipe this mod ships. Profiles are auto-filled at load: the normal
  // baseline is read from the live recipe and the cheap profile is scaled (half cost), both editable.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new()
    {
      // Blast-furnace components. The core carries four "side" variants where the door it replaced had
      // none, so the matcher needs the trailing wildcard.
      ["blastfurnacecore-grid"] = Grid("iwex:blastfurnacecore-*"),
      ["blastfurnace-tuyere-grid"] = Grid("iwex:tuyere-tuyere-*"),
      ["blastfurnacetap-grid"] = Grid("iwex:moltenmetaltap-*"),

      // Molten transport: canals, taps, the mold pedestal and the standalone barrel.
      ["moltenbarrel-grid"] = Grid("iwex:moltenbarrel"),
      ["moltencanal-start-grid"] = Grid("iwex:moltencanal-start-*"),
      ["moltencanal-straight-grid"] = Grid("iwex:moltencanal-straight-*"),
      ["moltencanal-bend-grid"] = Grid("iwex:moltencanal-bend-*"),
      ["moltencanal-tjunction-grid"] = Grid("iwex:moltencanal-tjunction-*"),
      ["moltencanal-xjunction-grid"] = Grid("iwex:moltencanal-xjunction-*"),
      ["moltencanal-tap-grid"] = Grid("iwex:moltencanal-tap-*"),
      ["moltencanal-moldpedestal-grid"] = Grid(
        "iwex:moltencanal-moldpedestal-*"
      ),

      // Slag paths - the by-product building set.
      ["slagpath-grid"] = Grid("iwex:slagpath-*"),
      ["slagpathslab-grid"] = Grid("iwex:slagpathslab-*"),
      ["slagpathstairs-grid"] = Grid("iwex:slagpathstairs-*"),
    };
}
