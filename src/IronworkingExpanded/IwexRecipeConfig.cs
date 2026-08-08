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
/// Each mod owns the costs of the content it ships. These entries moved here from
/// <c>SmexRecipeConfig</c> along with the molten, blast-furnace and slag-path subsystems: matching an
/// <c>iwex:</c> output from the steel catalogue worked, but it meant the iron tier had no cost switch of
/// its own - a player who wanted cheaper ironmaking had to discount steelmaking with it.
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
      ["blastfurnacecore-grid"] = Grid("iwex:furnace-blastcore-*"),
      ["blastfurnace-tuyere-grid"] = Grid("iwex:furnace-tuyere-*"),
      // The tap split in two. The key stays `blastfurnacetap-grid` for the iron notch so
      // a player's edited ex_recipes.json is not orphaned; the cinder notch is its own row.
      ["blastfurnacetap-grid"] = Grid("iwex:furnace-irontap-*"),
      ["blastfurnaceslagtap-grid"] = Grid("iwex:furnace-slagtap-*"),

      // Molten transport: canals, taps, the mold pedestal and the standalone barrel.
      // Wildcarded: the barrel carries a construction(plated|cast) group, so the bare code matches
      // nothing and an unwildcarded cost row is silently inert. The catalogue key is unchanged, so a
      // player's edited ex_recipes.json is not orphaned.
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
      // The brick set is a separate product line from the paths, and the `slag-path-*` selector above
      // does not reach it - `slag-bricks` and `slag-brick{slab,stairs}` share no prefix with it.
      ["slagbricks-grid"] = Grid("iwex:slag-bricks"),
      ["slagbrickslab-grid"] = Grid("iwex:slag-brickslab-*"),
      ["slagbrickstairs-grid"] = Grid("iwex:slag-brickstairs-*"),

      // Every craftable family needs a row - the coverage assertion enforces it. The overlap check
      // alone cannot: it only flags overlapping selectors, so a block with no row at all passes it,
      // silently exempt from the mod's economy switch. Adding a row is behaviour-neutral at the
      // `normal` level - the baseline is read from the live recipe - so a new row changes no shipped
      // cost; it only lets `/exmod recipes iwex cheap` reach the content.
      ["cupolacore-grid"] = Grid("iwex:furnace-cupolacore-*"),
      ["twintubblower-grid"] = Grid("iwex:furnace-twintubblower-*"),
      ["hoppertall-grid"] = Grid("iwex:hopper-tall-*"),

      // The casting stations.
      ["sandcastingcell-grid"] = Grid("iwex:casting-sandcell-*"),
      ["sandcastinglongcell-grid"] = Grid("iwex:casting-sandlongcell-*"),
      ["sandcastingbed-grid"] = Grid("iwex:casting-sandbed-*"),

      // Ore handling - one machine, the burdenmaker.
      // The trailing wildcard is required - the burdenmaker carries the `brick` variant group, so the
      // bare code matches nothing and the row would be silently inert (the same failure the barrel row
      // above guards against). Deleting this row is not subtle: the coverage assertion reports seven
      // uncovered codes, one per brick colour.
      // A player's edited ex_recipes.json may still carry retired `oremixer-grid` / `orebunker-grid`
      // keys; ExRecipeCosts skips a row whose selector matches nothing, so they are inert rather than
      // an error.
      ["burdenmaker-grid"] = Grid("iwex:burdenmaker-*"),

      // Plated pipe - the iron tier's pipe set, and the first of the three tiers.
      ["pipe-straight-grid"] = Grid("iwex:pipe-straight-*"),
      ["pipe-bend-grid"] = Grid("iwex:pipe-bend-*"),
      ["pipe-tjunction-grid"] = Grid("iwex:pipe-tjunction-*"),
      ["pipe-xjunction-grid"] = Grid("iwex:pipe-xjunction-*"),

      // Mechanical energy: shafting, flywheels and the gear transmissions.
      ["mpenergy-shaft-grid"] = Grid("iwex:mpenergy-shaft-*"),
      // `flywheel-normal` and `flywheel-large` are two families, and a bare `flywheel-*` would match
      // both - which the overlap check would then flag against either specific row.
      ["mpenergy-flywheel-grid"] = Grid("iwex:mpenergy-flywheel-normal-*"),
      ["mpenergy-flywheellarge-grid"] = Grid("iwex:mpenergy-flywheel-large-*"),
      ["mpenergy-transmission-x2-grid"] = Grid("iwex:mpenergy-transmission-x2-*"),
      ["mpenergy-transmission-x4-grid"] = Grid("iwex:mpenergy-transmission-x4-*"),
      ["mpenergy-transmission-clutch-grid"] = Grid(
        "iwex:mpenergy-transmission-clutch-*"
      ),

      // The shop floor.
      ["designtable-grid"] = Grid("iwex:crafting-designtable-*"),
      ["rollingmill-grid"] = Grid("iwex:forming-rollingmill-*"),
    };
}
