using ExpandedLib.Materials;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Seeds <see cref="MaterialRoleRegistry"/> with iiex's default material-role assignments: the headless
/// equivalent of loading <c>assets/iiex/config/materialroles.json</c>, which the test harness has no
/// asset pipeline to read. Mirrors that file entry for entry, and the parity tests assert the same codes
/// and values. Call once from a test assembly's module initializer. Idempotent.
/// <para>
/// The mod-gated ore codes (IndustrialStory / EM) are omitted: they only register when their mod is
/// enabled, which never happens headless.
/// </para>
/// </summary>
public static class MaterialRoleSeeds {
  private static bool _seeded;

  /// <summary>Idempotently registers the iiex default roles (clearing first for a clean slate).</summary>
  public static void SeedIiexDefaults() {
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
    // `assets/iiex/config/materialroles.json`, which
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
    // Our own bits carry the scrap role too. They have to: cast and pig iron pay out in their own bits
    // rather than in vanilla's, so without these rows the metal a chisel recovers is metal the cupola
    // refuses - a dead end where a leak used to be.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef {
        Role = Roles.Scrap,
        Code = "iiex:metalbit-castiron",
      }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iiex:metalbit-pigiron" }
    );
    // The pig family carries the scrap role: the cupola charges pig and scrap directly, so without
    // these three rows a headless cupola accepts nothing and can never melt.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iiex:pig" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iiex:pigchunk" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iiex:pigbit" }
    );
    // No `charge` grant: prepared burden is recognised by its own item identity (`Burden.IsAny`),
    // never by a role. `Roles.Charge` stays defined in exlib as a taxonomy entry other mods may grant.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" }
    );
  }
}
