using ExpandedLib.Materials;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Compat;

/// <summary>
/// Cross-mod compat for the blast furnace's iron-ore feed. The base <c>crushed-iron</c> prefix and the
/// other role assignments ship as JSON (<c>assets/iwex/config/materialroles.json</c>); this class stays
/// as the one thing JSON cannot express - the <b>mod-gated</b> ore codes, registered into
/// <see cref="Roles.IronOre"/> only when the contributing mod is present.
/// <para>It plugs into <see cref="MaterialRoleRegistry"/> as a code contributor: <see cref="Init"/>
/// registers <see cref="Contribute"/>, which the loader re-runs after every world load (so the
/// registrations survive the loader's clear). To add support for a new mod, extend
/// <see cref="Contribute"/> with an <c>IsModEnabled</c> branch that registers the new ore paths.</para>
/// </summary>
public static class IronOreCompat
{
  /// <summary>Registers the mod-gated ore contributor. Call once from the mod's <c>Start</c>.</summary>
  public static void Init(ICoreAPI api) =>
    MaterialRoleRegistry.RegisterContributor(Contribute);

  /// <summary>
  /// True if <paramref name="path"/> is a recognised crushed-iron-ore item path for the blast furnace
  /// feed - resolved through <see cref="MaterialRoleRegistry"/> (the JSON <c>crushed-iron</c> prefix plus
  /// any mod-gated code registered by <see cref="Contribute"/>). Callers pass the item's
  /// <see cref="AssetLocation.Path"/>; matching is domain-blind, reproducing the old prefix/set test.
  /// </summary>
  public static bool IsCrushedIronOre(string path) =>
    MaterialRoleRegistry.IsRole(Roles.IronOre, new AssetLocation(path));

  // The mod-gated registrations, re-applied after every load. Registered by exact (bare) code so a match
  // reproduces the old ExtraIronOrePaths.Contains(path) test - callers query by Code.Path (domain
  // stripped), which the registry re-homes to the game domain the same way these codes normalise.
  private static void Contribute(ICoreAPI api)
  {
    // IndustrialStory crushed iron ores.
    if (api.ModLoader.IsModEnabled("industrialstory"))
    {
      RegisterOre("crushed-hematite");
      RegisterOre("crushed-magnetite");
      RegisterOre("roasted-crushed-iron");
    }

    // Expanded Matter per-ore crushed iron variants (all smelt to ironbloom).
    if (api.ModLoader.IsModEnabled("em"))
    {
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
