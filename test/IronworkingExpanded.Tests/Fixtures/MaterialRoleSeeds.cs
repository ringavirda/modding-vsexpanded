using ExpandedLib.Materials;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Seeds <see cref="MaterialRoleRegistry"/> with iwex's default material-role assignments: the headless
/// equivalent of loading <c>assets/iwex/config/materialroles.json</c>, which the test harness has no
/// asset pipeline to read. Mirrors that file entry for entry, and the parity tests assert the same codes
/// and values. Call once from a test assembly's module initializer. Idempotent.
/// <para>
/// The mod-gated ore codes (IndustrialStory / EM) are omitted: they only register when their mod is
/// enabled, which never happens headless.
/// </para>
/// </summary>
public static class MaterialRoleSeeds {
  private static bool _seeded;

  /// <summary>Idempotently registers the iwex default roles (clearing first for a clean slate).</summary>
  public static void SeedIwexDefaults() {
    if (_seeded)
      return;
    _seeded = true;

    MaterialRoleRegistry.Clear();
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Flux, Code = "game:lime" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef {
        Role = Roles.Fuel,
        Code = "game:coke",
        Value = 2f,
      }
    );
    // The carbon a unit of this fuel carries: 1.0 against coke's 2.0, a 2:1 ratio. Read by the shaft
    // furnace's raceway burn (`BlockEntityFurnaceCore.CarbonPerUnit`). Must match the shipped
    // `assets/iwex/config/materialroles.json`, which
    // `FuelRoleGrantTests.Seed_matches_the_shipped_materialroles_json` asserts: a drifted seed makes
    // every headless furnace burn at a rate no world ever sees.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef {
        Role = Roles.Fuel,
        Code = "game:charcoal",
        Value = 1f,
      }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "game:metalbit-iron" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "game:metalbit-steel" }
    );
    // The pig family carries the scrap role: the cupola charges pig and scrap directly, so without
    // these three rows a headless cupola accepts nothing and can never melt.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pig" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pigchunk" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pigbit" }
    );
    // No `charge` grant: prepared burden is recognised by its own item identity (`Burden.IsAny`),
    // never by a role. `Roles.Charge` stays defined in exlib as a taxonomy entry other mods may grant.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" }
    );
  }
}
