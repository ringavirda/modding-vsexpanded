using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/iwex/lpex) is touched by the runner's reflection-based discovery, and seeds
/// the material-role registry the cupola's charge model reads through.
/// </summary>
internal static class ModuleInit
{
  [ModuleInitializer]
  internal static void Init()
  {
    VsAssemblyResolver.Register();
    TestLang.Init();

    // Note: this suite was green without the seeding below - which is exactly what made it a trap.
    // The cupola is an iwex `BlockEntityShaftFurnace`, so every question it asks about its own charge
    // (`IsFuelCode`, `CarbonPerUnit`, `IsChargeItem`) goes through `MaterialRoleRegistry` - a process-wide
    // static that holds nothing until somebody seeds it. lpex was getting iwex's roles by accident: this
    // project references IronworkingExpanded.Tests (for `PipeTestWorld`), and the first touch of any type
    // in that assembly fires its [ModuleInitializer], which seeds them. The roles therefore arrived only
    // as a side effect of an unrelated using - and would have vanished the day the pipe builders moved.
    //
    // The failure it closes is silent and mis-blames the machine. With no `fuel` role granted, every
    // coke band in a cupola weighs zero carbon, `RacewayHoldsCarbon` is false forever, and the furnace
    // sits at Idle however it is charged or blown - so `A_charged_lit_shaft_fed_blast_ignites` reads
    // "expected Firing, was Idle" and points at the furnace rather than at an empty registry. Seeding here
    // makes the suite own its own preconditions. Idempotent, so both initializers firing is harmless.
    MaterialRoleSeeds.SeedIwexDefaults();
  }
}
