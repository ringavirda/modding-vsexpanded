using ExpandedLib.Materials;

namespace IronworkingExpanded.Tests;

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
    // 1.0 against coke's 2.0 - a 2:1 ratio, retuned from 0.5. It is the carbon a unit of
    // this fuel carries. It was once read by the ore mixer's carbon tax too; the mixer is gone and
    // the shaft furnace's raceway burn (`BlockEntityFurnaceCore.CarbonPerUnit`) is the reader that matters. The value and the
    // shipped `assets/iwex/config/materialroles.json` must agree -
    // `FuelRoleGrantTests.Seed_matches_the_shipped_materialroles_json` asserts it, because a seed that
    // drifts makes every headless furnace burn at a rate no world ever sees.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef
      {
        Role = Roles.Fuel,
        Code = "game:charcoal",
        Value = 1f,
      }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "game:metalbit-iron" }
    );
    // Was missing for a while, during which this class's own doc claimed it mirrored the asset
    // "entry-for-entry". The shipped file has granted it since scrap gained a second code; nothing
    // asserted the correspondence, so headless steel bits simply were not scrap.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "game:metalbit-steel" }
    );
    // The pig family carries the scrap role, and it is what makes the cupola reachable at
    // all: the ore mixer that made remelt burden is gone, so the cupola charges pig and scrap
    // directly now. Without these three rows a headless cupola accepts nothing, and its whole scenario
    // suite would arrange a furnace that can never melt.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pig" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pigchunk" }
    );
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.Scrap, Code = "iwex:pigbit" }
    );
    // The `charge` role's single grant (`iwex:blastmix`) is gone with the item. Prepared
    // burden is recognised by its own item identity (`Burden.IsAny`), never by a role, so nothing
    // replaces this row. `Roles.Charge` itself stays defined in exlib - it is a taxonomy entry other
    // mods may grant - it simply has no iwex grant any more.
    MaterialRoleRegistry.Register(
      new MaterialRoleDef { Role = Roles.IronOre, PathPrefix = "crushed-iron" }
    );
  }
}
