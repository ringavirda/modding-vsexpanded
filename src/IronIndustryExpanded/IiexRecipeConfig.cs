using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using ExpandedLib.Registries.Recipes;

namespace IronIndustryExpanded;

/// <summary>
/// The iron-industry recipe cost catalogue, written to the <c>iiex</c> section of
/// <c>ModConfig/ex_recipes.json</c> alongside the main <c>ex_values.json</c>. Same shape and behaviour
/// as every other mod's catalogue: each entry names a grid recipe (by output) or an RCC construction
/// (by block) to manage; the <c>normal</c> level is auto-filled from the recipes as authored, and the
/// <c>cheap</c> level is scale-filled from it (then editable). The active level is chosen by
/// <c>/exmod recipes iiex &lt;level&gt;</c> (stored in <see cref="IiexConfig.RecipeLevel"/>) and applied
/// on the next world reload. Each mod owns the costs of the content it ships, so the iron tier has its
/// own switch independent of steelmaking's.
/// <para>
/// The two pipe tiers need distinct keys. Both merged catalogues spelled theirs
/// <c>pipe-straight-grid</c>, and the collection initialiser assigns through the indexer, so one tier's
/// four rows would have overwritten the other's with no duplicate-key error and no failing test - the
/// overwritten tier simply drops out of the economy switch.
/// </para>
/// </summary>
[ExConfigRegister(
  "ex_recipes.json",
  "iiex",
  LegacyFileNames = new string[] { "iwex_recipes.json", "lpex_recipes.json" }
)]
public class IiexRecipeConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  private Dictionary<string, RecipeCostEntry>? _recipes;

  /// <summary>Never null: a missing or null <c>Recipes</c> in the file falls back to the code
  /// defaults, so a bad edit cannot blank the catalogue. Missing or broken individual entries are
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

  // Grid recipe with a pinned cheap-profile output count. Authored output is straight 2 and
  // bend/t/x-junction 1; the cheap profile doubles those to 4/2/2/2.
  private static RecipeCostEntry GridOut(string match, int cheapOutput) =>
    new() {
      Type = "grid",
      Match = match,
      Profiles = new() { ["cheap"] = new() { Quantity = cheapOutput } },
    };

  // Curated list of every grid and RCC recipe this mod ships. Profiles are auto-filled at load: the
  // normal baseline is read from the live recipe and the cheap profile is scaled (half cost), both
  // editable. Only the cheap cast-pipe output is pinned.
  private static Dictionary<string, RecipeCostEntry> Defaults() =>
    new() {
      // Blast-furnace components. The core carries four `side` variants, so the matcher needs the
      // trailing wildcard.
      ["blastfurnacecore-grid"] = Grid("iiex:furnace-blastcore-*"),
      ["blastfurnace-tuyere-grid"] = Grid("iiex:furnace-tuyere-*"),
      // `blastfurnacetap-grid` names the iron notch and the cinder notch has its own row. Renaming a
      // key orphans the matching row in a player's edited ex_recipes.json.
      ["blastfurnacetap-grid"] = Grid("iiex:furnace-irontap-*"),
      ["blastfurnaceslagtap-grid"] = Grid("iiex:furnace-slagtap-*"),

      // Molten transport: canals, taps, the mold pedestal and the standalone barrel. The barrel
      // carries a construction(plated|cast) group, so the bare code matches nothing and an
      // unwildcarded cost row is silently inert.
      ["moltenbarrel-grid"] = Grid("iiex:molten-barrel-*"),
      ["moltencanal-start-grid"] = Grid("iiex:molten-canal-start-*"),
      ["moltencanal-straight-grid"] = Grid("iiex:molten-canal-straight-*"),
      ["moltencanal-bend-grid"] = Grid("iiex:molten-canal-bend-*"),
      ["moltencanal-tjunction-grid"] = Grid("iiex:molten-canal-tjunction-*"),
      ["moltencanal-xjunction-grid"] = Grid("iiex:molten-canal-xjunction-*"),
      ["moltencanal-tap-grid"] = Grid("iiex:molten-canal-tap-*"),
      ["moltencanal-moldpedestal-grid"] = Grid(
        "iiex:molten-canal-moldpedestal-*"
      ),

      // Slag paths - the by-product building set.
      ["slagpath-grid"] = Grid("iiex:slag-path-*"),
      ["slagpathslab-grid"] = Grid("iiex:slag-pathslab-*"),
      ["slagpathstairs-grid"] = Grid("iiex:slag-pathstairs-*"),
      // `slag-bricks` and `slag-brick{slab,stairs}` share no prefix with the `slag-path-*` selectors
      // above, so the brick set needs its own rows.
      ["slagbricks-grid"] = Grid("iiex:slag-bricks"),
      ["slagbrickslab-grid"] = Grid("iiex:slag-brickslab-*"),
      ["slagbrickstairs-grid"] = Grid("iiex:slag-brickstairs-*"),

      // Every craftable family needs a row: the coverage assertion enforces it, and the overlap check
      // only flags overlapping selectors, so a family with no row is silently exempt from the economy
      // switch. Adding a row is cost-neutral at `normal`, whose baseline is read from the live recipe.
      ["cupolacore-grid"] = Grid("iiex:furnace-cupolacore-*"),
      ["twintubblower-grid"] = Grid("iiex:furnace-twintubblower-*"),
      ["hoppertall-grid"] = Grid("iiex:hopper-tall-*"),

      // The casting stations.
      ["sandcastingcell-grid"] = Grid("iiex:casting-sandcell-*"),
      ["sandcastinglongcell-grid"] = Grid("iiex:casting-sandlongcell-*"),
      ["sandcastingbed-grid"] = Grid("iiex:casting-sandbed-*"),

      // Ore handling - one machine, the burdenmaker. The trailing wildcard is required: it carries the
      // `brick` variant group, so the bare code matches nothing and the row would be silently inert.
      // ExRecipeCosts skips a row whose selector matches nothing, so a retired key left in a player's
      // edited ex_recipes.json is inert rather than an error.
      ["burdenmaker-grid"] = Grid("iiex:burdenmaker-*"),

      // Plated pipe - the bootstrap tier, hammered from plate before steam exists.
      ["pipe-plated-straight-grid"] = Grid("iiex:pipe-plated-straight-*"),
      ["pipe-plated-bend-grid"] = Grid("iiex:pipe-plated-bend-*"),
      ["pipe-plated-tjunction-grid"] = Grid("iiex:pipe-plated-tjunction-*"),
      ["pipe-plated-xjunction-grid"] = Grid("iiex:pipe-plated-xjunction-*"),

      // Mechanical energy: shafting, flywheels and the gear transmissions.
      ["mpenergy-shaft-grid"] = Grid("iiex:mpenergy-shaft-*"),
      // `flywheel-normal` and `flywheel-large` are separate families; a bare `flywheel-*` would match
      // both and the overlap check would flag it against either specific row.
      ["mpenergy-flywheel-grid"] = Grid("iiex:mpenergy-flywheel-normal-*"),
      ["mpenergy-flywheellarge-grid"] = Grid("iiex:mpenergy-flywheel-large-*"),
      ["mpenergy-transmission-x2-grid"] = Grid(
        "iiex:mpenergy-transmission-x2-*"
      ),
      ["mpenergy-transmission-x4-grid"] = Grid(
        "iiex:mpenergy-transmission-x4-*"
      ),
      ["mpenergy-transmission-clutch-grid"] = Grid(
        "iiex:mpenergy-transmission-clutch-*"
      ),

      // The shop floor.
      ["designtable-grid"] = Grid("iiex:crafting-designtable-*"),
      ["rollingmill-grid"] = Grid("iiex:forming-rollingmill-*"),

      // RCC constructions (the heavy multiblock build costs). The Lancashire boiler and Cornish
      // engine are catalogued in HpexRecipeConfig.
      ["boilercornish-rcc"] = Rcc("iiex:boilercornish-*"),
      ["enginewatt-rcc"] = Rcc("iiex:enginewatt-*"),

      // Steam machine grid "frame" recipes.
      ["boilercornish-grid"] = Grid("iiex:boilercornish-*"),
      ["enginewatt-grid"] = Grid("iiex:enginewatt-*"),
      ["enginefluidpump-grid"] = Grid("iiex:enginefluidpump-*"),
      ["enginempgenerator-grid"] = Grid("iiex:enginempgenerator-*"),
      ["manualfluidpump-grid"] = Grid("iiex:manualfluidpump-*"),
      ["steamcondenser-grid"] = Grid("iiex:steamcondenser-*"),

      // Cast pipe - the steam tier's pipe set. Straight, bend and junctions yield double in the
      // cheap profile.
      ["pipe-cast-straight-grid"] = GridOut("iiex:pipe-cast-straight-*", 4),
      ["pipe-cast-bend-grid"] = GridOut("iiex:pipe-cast-bend-*", 2),
      ["pipe-cast-tjunction-grid"] = GridOut("iiex:pipe-cast-tjunction-*", 2),
      ["pipe-cast-xjunction-grid"] = GridOut("iiex:pipe-cast-xjunction-*", 2),
      ["pipe-fluidintake-grid"] = Grid("iiex:pipe-fluidintake-*"),
      ["pipe-outlet-grid"] = Grid("iiex:pipe-outlet-*"),
      ["pipe-passthrough-grid"] = Grid("iiex:pipe-cast-passthrough-*"),
      ["pipe-passthroughbend-grid"] = Grid("iiex:pipe-cast-passthroughbend-*"),
      ["pipe-valve-grid"] = Grid("iiex:pipe-cast-valve-*"),
      ["pipe-pressurevalve-grid"] = Grid("iiex:pipe-cast-pressurevalve-*"),
    };
}
