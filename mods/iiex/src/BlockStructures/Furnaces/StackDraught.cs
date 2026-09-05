using System;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// What a chimney pulls. A naturally-aspirated furnace has no blower, so the only thing standing between
/// its fire and a cold hearth is the stack: draught force rises with height, and flue friction and stack
/// heat loss rise faster, so the curve has a real optimum and declines past it. The damper and the doors
/// are the operating inputs on top of that.
/// </summary>
/// <remarks>
/// Pure and world-free, like <see cref="HearthRows"/> and <see cref="PuddlingHearthLayout"/>. The value it
/// returns is an air factor - the same 0-1 multiplier a blown furnace gets from its tuyeres - so
/// <c>ComputeHeatBalance</c> reads it in the one place it already reads
/// <c>IiexValues.BfNaturalDraughtFactor</c>. See <c>docs/design/machines/crucible-furnace.md</c>.
/// </remarks>
public static class StackDraught {
  /// <summary>
  /// The air factor a stack of <paramref name="courses"/> pulls:
  /// <c>base + gain·√courses − friction·courses²</c>, then scaled by whatever the damper and the doors
  /// are doing. A bare flue pulls the base factor, which is what an unstacked furnace has always had.
  /// </summary>
  /// <param name="courses">Flue courses above the fire. Negative counts read as none.</param>
  /// <param name="damperOpen">Whether the chimney damper stands open.</param>
  /// <param name="venting">Whether a charge door is open, spilling the pull into the room.</param>
  /// <remarks>
  /// No clamp on the way up. A cap would be invisible - courses past it would change nothing, with no
  /// feedback - while a decline is observable and is the honest physics. Building past the peak has to
  /// ruin the furnace rather than merely stop helping it, which is also why the block info names the
  /// count and the peak.
  /// </remarks>
  public static float NaturalDraughtFor(
    int courses,
    bool damperOpen = true,
    bool venting = false
  ) {
    float n = Math.Max(0, courses);
    float draught =
      IiexValues.BfNaturalDraughtFactor
      + IiexValues.StackDraughtGain * (float)Math.Sqrt(n)
      - IiexValues.StackDraughtFriction * n * n;

    if (!damperOpen)
      draught *= IiexValues.StackDamperShutFactor;
    if (venting)
      draught *= IiexValues.StackVentingFactor;

    // The floor is not a design cap but a physical one: a furnace whose stack has been built so far past
    // the peak that the arithmetic goes negative still draws no less than nothing.
    return GameMath.Clamp(draught, 0f, 1f);
  }
}
