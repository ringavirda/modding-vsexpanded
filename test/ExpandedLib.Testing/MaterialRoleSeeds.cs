using ExpandedLib.Materials;

namespace ExpandedLib.Testing;

/// <summary>
/// Seeds <see cref="MaterialRoleRegistry"/> with iwex's default material-role assignments - the
/// headless equivalent of loading <c>assets/iwex/config/materialroles.json</c>, which the test harness
/// has no asset pipeline to read. Mirrors that file entry-for-entry (kept in sync by the parity tests,
/// which assert the same codes/values). Call once from a test assembly's module initializer so the
/// mixer / hopper / furnace-core classification the migrated code now reads through the registry
/// resolves exactly as the old hardcoded checks did. Idempotent.
/// <para>
/// The mod-gated ore codes (IndustrialStory / EM) are deliberately omitted - they only register when
/// their mod is enabled, which never happens headless, so leaving them out reproduces the base game.
/// </para>
/// </summary>
public static class MaterialRoleSeeds
{
  private static bool _seeded;

  /// <summary>Idempotently registers the iwex default roles (clearing first for a clean slate).</summary>
  public static void SeedIwexDefaults()
  {
    if (_seeded)
      return;
    _seeded = true;

    MaterialRoleRegistry.Clear();
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Fuel, Code = "game:coke", Value = 2f }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef
      {
        Role = Roles.Fuel,
        Code = "game:charcoal",
        Value = 0.5f,
      }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "game:metalbit-iron" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Charge, Code = "iwex:blastmix" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" }
    );
  }
}
