using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// Drives the production process a machine carries, without naming the class that carries it. A form
/// notices a state change - a pattern completed, a breach - and says so here; a machine that runs no
/// process is simply unaffected, which is what lets a form be form only. The counterpart of
/// <see cref="ProductionReadiness"/>: readiness is what a form publishes, this is what it commands.
/// See docs/design/mechanics/framework-composition.md.
/// </summary>
public static class ProductionProcess {
  /// <summary>Every process <paramref name="be"/> carries; empty when it carries none.</summary>
  public static IEnumerable<BEBehaviorProductionMachine> ProcessesOn(
    BlockEntity? be
  ) => be == null ? [] : be.Behaviors.OfType<BEBehaviorProductionMachine>();

  /// <summary>Registers the production tick of every process on <paramref name="be"/>. Idempotent, and
  /// server-side only: each process applies that rule itself.</summary>
  public static void Start(BlockEntity? be) {
    foreach (BEBehaviorProductionMachine process in ProcessesOn(be))
      process.StartProductionTick();
  }

  /// <summary>Unregisters the production tick of every process on <paramref name="be"/>, which stops the
  /// machine rather than idling it: the gate is only read by a listener that still exists.</summary>
  public static void Stop(BlockEntity? be) {
    foreach (BEBehaviorProductionMachine process in ProcessesOn(be))
      process.StopProductionTick();
  }
}
