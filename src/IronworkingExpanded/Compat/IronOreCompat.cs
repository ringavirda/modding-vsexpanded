using ExpandedLib.Materials;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Compat;

/// <summary>
/// Cross-mod compat for the blast furnace's iron-ore feed. The base <c>crushed-iron</c> prefix and the
/// other role assignments ship as JSON (<c>assets/iwex/config/materialroles.json</c>); this class holds
/// only the mod-gated ore codes, registered into <see cref="Roles.IronOre"/> when the contributing mod is
/// present. It plugs into <see cref="MaterialRoleRegistry"/> as a code contributor: <see cref="Init"/>
/// registers <see cref="Contribute"/>, which the loader re-runs after every world load so the
/// registrations survive the loader's clear. Supporting a further mod means adding an
/// <c>IsModEnabled</c> branch to <see cref="Contribute"/>.
/// </summary>
public static class IronOreCompat {
  /// <summary>Registers the mod-gated ore contributor. Call once from the mod's <c>Start</c>.</summary>
  public static void Init(ICoreAPI api) =>
    MaterialRoleRegistry.RegisterContributor(Contribute);

  /// <summary>
  /// True if <paramref name="path"/> is a recognised crushed-iron-ore item path for the blast furnace
  /// feed, resolved through <see cref="MaterialRoleRegistry"/>: the JSON <c>crushed-iron</c> prefix plus
  /// any mod-gated code registered by <see cref="Contribute"/>. Callers pass the item's
  /// <see cref="AssetLocation.Path"/>; matching is domain-blind.
  /// </summary>
  public static bool IsCrushedIronOre(string path) =>
    MaterialRoleRegistry.IsRole(Roles.IronOre, new AssetLocation(path));

  // The mod-gated registrations, re-applied after every load. Registered by exact bare code: callers query
  // by Code.Path with the domain stripped, and the registry re-homes both to the game domain, so the two
  // normalise to the same key.
  private static void Contribute(ICoreAPI api) {
    // IndustrialStory crushed iron ores.
    if (api.ModLoader.IsModEnabled("industrialstory")) {
      RegisterOre("crushed-hematite");
      RegisterOre("crushed-magnetite");
      RegisterOre("roasted-crushed-iron");
    }

    // Expanded Matter per-ore crushed iron variants (all smelt to ironbloom).
    if (api.ModLoader.IsModEnabled("em")) {
      RegisterOre("crushed-ore-hematite");
      RegisterOre("crushed-ore-limonite");
      RegisterOre("crushed-ore-magnetite");
    }
  }

  private static void RegisterOre(string path) =>
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, Code = path }
    );
}
