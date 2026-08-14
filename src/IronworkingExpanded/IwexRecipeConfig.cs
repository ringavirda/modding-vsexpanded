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
/// <see cref="IwexConfig.RecipeLevel"/>) and applied on the next world reload. Each mod owns the costs of
/// the content it ships, so the iron tier has its own switch independent of steelmaking's.
/// </summary>
[ExConfigRegister("ex_recipes.json", "iwex")]
public class IwexRecipeConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  private Dictionary<string, RecipeCostEntry>? _recipes;

  /// <summary>Never null: a missing or null <c>Recipes</c> in the file falls back to the code
  /// defaults. Missing/broken individual entries are repaired against <see cref="DefaultCatalogue"/>
  /// at load by <see cref="ExRecipeCosts.Reconcile"/>.</summary>
  public Dictionary<string, RecipeCostEntry> Recipes {
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
    new() {
      // Blast-furnace components. The core carries four `side` variants, so the matcher needs the
      // trailing wildcard.
      ["blastfurnacecore-grid"] = Grid("iwex:furnace-blastcore-*"),
      ["blastfurnace-tuyere-grid"] = Grid("iwex:furnace-tuyere-*"),
      // `blastfurnacetap-grid` names the iron notch and the cinder notch has its own row. Renaming a
      // key orphans the matching row in a player's edited ex_recipes.json.
      ["blastfurnacetap-grid"] = Grid("iwex:furnace-irontap-*"),
      ["blastfurnaceslagtap-grid"] = Grid("iwex:furnace-slagtap-*"),

      // Molten transport: canals, taps, the mold pedestal and the standalone barrel. The barrel
      // carries a construction(plated|cast) group, so the bare code matches nothing and an
      // unwildcarded cost row is silently inert.
      ["moltenbarrel-grid"] = Grid("iwex:molten-barrel-*"),
      ["moltencanal-start-grid"] = Grid("iwex:molten-canal-start-*"),
      ["moltencanal-straight-grid"] = Grid("iwex:molten-canal-straight-*"),
      ["moltencanal-bend-grid"] = Grid("iwex:molten-canal-bend-*"),
      ["moltencanal-tjunction-grid"] = Grid("iwex:molten-canal-tjunction-*"),
      ["moltencanal-xjunction-grid"] = Grid("iwex:molten-canal-xjunction-*"),
      ["moltencanal-tap-grid"] = Grid("iwex:molten-canal-tap-*"),
      ["moltencanal-moldpedestal-grid"] = Grid(
        "iwex:molten-canal-moldpedestal-*"
      ),

      // Slag paths - the by-product building set.
      ["slagpath-grid"] = Grid("iwex:slag-path-*"),
      ["slagpathslab-grid"] = Grid("iwex:slag-pathslab-*"),
      ["slagpathstairs-grid"] = Grid("iwex:slag-pathstairs-*"),
      // `slag-bricks` and `slag-brick{slab,stairs}` share no prefix with the `slag-path-*` selectors
      // above, so the brick set needs its own rows.
      ["slagbricks-grid"] = Grid("iwex:slag-bricks"),
      ["slagbrickslab-grid"] = Grid("iwex:slag-brickslab-*"),
      ["slagbrickstairs-grid"] = Grid("iwex:slag-brickstairs-*"),

      // Every craftable family needs a row: the coverage assertion enforces it, and the overlap check
      // only flags overlapping selectors, so a family with no row is silently exempt from the economy
      // switch. Adding a row is cost-neutral at `normal`, whose baseline is read from the live recipe.
      ["cupolacore-grid"] = Grid("iwex:furnace-cupolacore-*"),
      ["twintubblower-grid"] = Grid("iwex:furnace-twintubblower-*"),
      ["hoppertall-grid"] = Grid("iwex:hopper-tall-*"),

      // The casting stations.
      ["sandcastingcell-grid"] = Grid("iwex:casting-sandcell-*"),
      ["sandcastinglongcell-grid"] = Grid("iwex:casting-sandlongcell-*"),
      ["sandcastingbed-grid"] = Grid("iwex:casting-sandbed-*"),

      // Ore handling - one machine, the burdenmaker. The trailing wildcard is required: it carries the
      // `brick` variant group, so the bare code matches nothing and the row would be silently inert.
      // ExRecipeCosts skips a row whose selector matches nothing, so a retired key left in a player's
      // edited ex_recipes.json is inert rather than an error.
      ["burdenmaker-grid"] = Grid("iwex:burdenmaker-*"),

      // Plated pipe - the iron tier's pipe set, first of the three tiers.
      ["pipe-straight-grid"] = Grid("iwex:pipe-plated-straight-*"),
      ["pipe-bend-grid"] = Grid("iwex:pipe-plated-bend-*"),
      ["pipe-tjunction-grid"] = Grid("iwex:pipe-plated-tjunction-*"),
      ["pipe-xjunction-grid"] = Grid("iwex:pipe-plated-xjunction-*"),

      // Mechanical energy: shafting, flywheels and the gear transmissions.
      ["mpenergy-shaft-grid"] = Grid("iwex:mpenergy-shaft-*"),
      // `flywheel-normal` and `flywheel-large` are separate families; a bare `flywheel-*` would match
      // both and the overlap check would flag it against either specific row.
      ["mpenergy-flywheel-grid"] = Grid("iwex:mpenergy-flywheel-normal-*"),
      ["mpenergy-flywheellarge-grid"] = Grid("iwex:mpenergy-flywheel-large-*"),
      ["mpenergy-transmission-x2-grid"] = Grid(
        "iwex:mpenergy-transmission-x2-*"
      ),
      ["mpenergy-transmission-x4-grid"] = Grid(
        "iwex:mpenergy-transmission-x4-*"
      ),
      ["mpenergy-transmission-clutch-grid"] = Grid(
        "iwex:mpenergy-transmission-clutch-*"
      ),

      // The shop floor.
      ["designtable-grid"] = Grid("iwex:crafting-designtable-*"),
      ["rollingmill-grid"] = Grid("iwex:forming-rollingmill-*"),
    };
}
