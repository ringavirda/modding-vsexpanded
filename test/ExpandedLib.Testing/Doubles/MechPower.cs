using System.Collections;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;

namespace ExpandedLib.Testing;

/// <summary>
/// Helpers for headless mechanical-power tests. <see cref="MechanicalNetwork"/> has a public
/// parameterless constructor with settable <c>Speed</c>/<c>NetworkResistance</c>, so a turning, stalled
/// or overloaded network can be built directly, bound onto a real <see cref="BEBehaviorMPBase"/> and
/// that behavior added to a block entity's behavior list so <c>GetBehavior&lt;BEBehaviorMPBase&gt;()</c>
/// resolves it. That wiring is done by the game at chunk load and skipped by the headless harness.
/// </summary>
public static class MechPower {
  /// <summary>A fake mechanical network turning at <paramref name="speed"/> with the given load.</summary>
  public static MechanicalNetwork Network(float speed, float resistance = 0f) =>
    new() { Speed = speed, NetworkResistance = resistance };

  /// <summary>
  /// Binds <paramref name="network"/> onto <paramref name="behavior"/>'s private <c>network</c> field
  /// and attaches the behavior to <paramref name="be"/> so the BE's <c>GetBehavior</c> resolves it.
  /// </summary>
  public static T Attach<T>(
    BlockEntity be,
    T behavior,
    MechanicalNetwork? network
  )
    where T : BEBehaviorMPBase {
    if (network != null)
      ReflectionHelpers.SetField(behavior, "network", network);
    var list = (IList)ReflectionHelpers.GetField(be, "Behaviors")!;
    list.Add(behavior);
    return behavior;
  }
}
