using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Machines;

/// <summary>
/// Reads the readiness a machine publishes. A machine may publish more than one answer - a pattern, a
/// construction stage, a resolved peer - so each question below is put to every publisher and reduced
/// to one answer for the machine. Publishers are the block entity and its behaviours, both of which
/// exist from construction, so readiness answers before <c>Initialize</c> runs and without a world
/// lookup. See docs/design/mechanics/framework-composition.md.
/// </summary>
public static class ProductionReadiness {
  /// <summary>Every readiness answer <paramref name="be"/> publishes; empty when it publishes none.</summary>
  public static IEnumerable<IProductionReadiness> PublishersOn(BlockEntity? be) {
    if (be == null)
      yield break;

    if (be is IProductionReadiness self)
      yield return self;

    foreach (
      IProductionReadiness behaviour in be.Behaviors.OfType<IProductionReadiness>()
    )
      yield return behaviour;
  }

  /// <summary>
  /// Whether every publisher on <paramref name="be"/> is ready. Readiness is a conjunction: a machine
  /// gated by both a built pattern and a finished construction is ready only once both say so. A
  /// machine that publishes nothing is ready, having declared no condition it could fail.
  /// </summary>
  public static bool IsReady(BlockEntity? be) =>
    PublishersOn(be).All(p => p.IsReadyToProduce);

  /// <summary>
  /// Whether losing readiness unregisters <paramref name="be"/>'s production tick. A single publisher
  /// answering <c>false</c> keeps the tick for the whole machine, because the two mistakes are not
  /// symmetric: a machine left ticking merely idles, while one stopped against its will freezes with
  /// its state held rather than stopping.
  /// </summary>
  public static bool StopsProductionWhenNotReady(BlockEntity? be) =>
    PublishersOn(be).All(p => p.StopsProductionWhenNotReady);
}
