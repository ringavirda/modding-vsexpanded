using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/iwex/lpex) is touched by the runner's reflection-based discovery, and seeds
/// the material-role registry the cupola's charge model reads through.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();

    // The cupola is an iwex BlockEntityShaftFurnace, so every question it asks about its own charge
    // (IsFuelCode, CarbonPerUnit, IsChargeItem) goes through MaterialRoleRegistry, a process-wide
    // static that holds nothing until seeded. Without a `fuel` role every coke band weighs zero
    // carbon, RacewayHoldsCarbon stays false and the furnace never leaves Idle however it is charged
    // or blown. Seeding here keeps the suite independent of whether IronworkingExpanded.Tests' own
    // module initializer has fired. Idempotent, so both firing is harmless.
    MaterialRoleSeeds.SeedIwexDefaults();
  }
}
