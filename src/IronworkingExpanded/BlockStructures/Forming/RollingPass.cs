using System;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// Physics of one pass through a two-high rolling stand. Pure static math with no world state, so the model
/// runs headless and the mill block is only a readout of it. Three relations carry it: the bite limit
/// <c>δ_max = μ²R</c>, roll force <c>F = Y·w·L_c</c> over the arc of contact <c>L_c = √(R·δ)</c>, and load
/// torque <c>T = 2·F·a</c> with lever arm <c>a ≈ L_c/2</c> over two rolls. Flow stress rises as the stock
/// cools, so temperature drives both whether a pass bites and whether the run can carry it.
/// See docs/design/machines/rolling-mill.md and docs/design/mechanics/mp-energy.md.
/// </summary>
public static class RollingPass {
  /// <summary>Friction coefficient of hot iron against a cast roll.</summary>
  public const float HotFriction = 0.5f;

  /// <summary>Friction coefficient of cold iron. Draft scales with μ², so a cold stand takes roughly a
  /// thirtieth of the draft a hot one does.</summary>
  public const float ColdFriction = 0.09f;

  /// <summary>
  /// Largest draft (thickness reduction) a stand of roll radius <paramref name="rollRadius"/> can pull in by
  /// friction alone: <c>δ_max = μ²R</c>. Above it the stock does not enter and the rolls skid on it.
  /// </summary>
  public static float MaxDraft(float rollRadius, float friction) =>
    rollRadius <= 0f || friction <= 0f ? 0f : friction * friction * rollRadius;

  /// <summary>
  /// Whether the rolls can bite a piece at <paramref name="tempC"/> for the requested
  /// <paramref name="draft"/>. Below <paramref name="rollingTempC"/> the stock counts as cold and its
  /// friction collapses, so a draft a hot piece takes is refused outright rather than allowed to stall.
  /// </summary>
  public static bool CanBite(
    float draft,
    float rollRadius,
    float tempC,
    float rollingTempC
  ) =>
    draft > 0f
    && draft <= MaxDraft(rollRadius, FrictionAt(tempC, rollingTempC));

  /// <summary>Friction at a given stock temperature: hot at or above the rolling heat, cold below it. A step,
  /// not a ramp - the scale film and recrystallisation it stands for change abruptly.</summary>
  public static float FrictionAt(float tempC, float rollingTempC) =>
    tempC >= rollingTempC ? HotFriction : ColdFriction;

  /// <summary>
  /// Average flow stress of the stock at <paramref name="tempC"/>, relative to its hot value. At and above the
  /// rolling heat it is <c>1</c>; below, it climbs linearly to <paramref name="coldMultiplier"/>, reaching the
  /// full multiplier once the stock is <paramref name="coldSpanC"/> degrees under.
  /// </summary>
  public static float FlowStress(
    float tempC,
    float rollingTempC,
    float coldMultiplier,
    float coldSpanC
  ) {
    if (tempC >= rollingTempC)
      return 1f;
    if (coldSpanC <= 0f)
      return coldMultiplier;
    float coldness = Math.Clamp((rollingTempC - tempC) / coldSpanC, 0f, 1f);
    return 1f + (MathF.Max(1f, coldMultiplier) - 1f) * coldness;
  }

  /// <summary>Arc of contact <c>L_c = √(R·δ)</c>: how much of the roll presses on the stock. Both the roll
  /// force and the lever arm derive from it.</summary>
  public static float ContactLength(float rollRadius, float draft) =>
    rollRadius <= 0f || draft <= 0f ? 0f : MathF.Sqrt(rollRadius * draft);

  /// <summary>
  /// Resisting torque (N·m) a pass imposes on the run: <c>T = 2·F·a</c> with <c>F = Y·w·L_c</c> and lever arm
  /// <c>a = L_c/2</c>, reducing to <c>T = Y·w·R·δ·k</c>. <paramref name="torqueScale"/> balances the mill
  /// against the flywheel from config without touching the physics.
  /// <para>
  /// Independent of shaft speed: a plastic-deformation load resists the same however fast the rolls turn, so
  /// it does not ease off as ω falls and can hold a run stalled.
  /// </para>
  /// </summary>
  public static float LoadTorque(
    float draft,
    float width,
    float rollRadius,
    float tempC,
    float rollingTempC,
    float coldMultiplier,
    float coldSpanC,
    float torqueScale
  ) {
    float contact = ContactLength(rollRadius, draft);
    if (contact <= 0f || width <= 0f)
      return 0f;
    float flowStress = FlowStress(
      tempC,
      rollingTempC,
      coldMultiplier,
      coldSpanC
    );
    // F = Y * w * L_c, and T = 2 * F * (L_c / 2) = F * L_c -> Y * w * L_c^2 = Y * w * R * delta.
    return flowStress * width * contact * contact * torqueScale;
  }

  /// <summary>
  /// Whether a run turning at <paramref name="speed"/> with <paramref name="availableTorque"/> on tap can carry
  /// <paramref name="loadTorque"/>. A shaft at rest cannot start a pass at any torque, so the run has to be
  /// spun up first.
  /// </summary>
  public static bool CanCarry(
    float loadTorque,
    float availableTorque,
    float speed
  ) => speed > 0f && availableTorque >= loadTorque;

  /// <summary>
  /// How wide a piece becomes once rolled from <paramref name="startThickness"/> down to
  /// <paramref name="thickness"/>: <c>w = w₀·(t₀/t)^e</c>, capped at <paramref name="maxWidth"/> when that is
  /// above 0. <paramref name="maxWidth"/> is the room between the housings.
  /// <para>
  /// The exponent splits the displaced metal between width (lateral spread) and length, which takes the
  /// complement <c>1-e</c> so volume is conserved. It is per-form because spread falls as stock gets wider
  /// relative to its thickness: friction across a wide face resists sideways flow. See <see cref="StockForm"/>.
  /// A piece wider than the barrel no longer fits one bite and is taken in side-by-side passes
  /// (<see cref="RollSetSpec.PassesAt"/>).
  /// </para>
  /// </summary>
  public static float SpreadWidth(
    float startWidth,
    float startThickness,
    float thickness,
    float exponent = 0.5f,
    float maxWidth = 0f
  ) {
    if (startWidth <= 0f)
      return 0f;
    if (startThickness <= 0f || thickness <= 0f)
      return startWidth;
    float width = startWidth * MathF.Pow(startThickness / thickness, exponent);
    return maxWidth > 0f ? MathF.Min(width, maxWidth) : width;
  }

  /// <summary>
  /// How much longer the piece gets, as a multiple of its starting length. Derived from the actual (possibly
  /// capped) width rather than from the spread exponent, so that whatever the reduction did not put into width
  /// goes into length: once a piece stops widening, every further reduction runs out lengthways.
  /// </summary>
  public static float LengthMultiplier(
    float startWidth,
    float startThickness,
    float thickness,
    float width
  ) =>
    startThickness <= 0f || thickness <= 0f || width <= 0f || startWidth <= 0f
      ? 1f
      : startThickness / thickness / (width / startWidth);

  /// <summary>
  /// Stock temperature after <paramref name="dt"/> seconds, cooling exponentially toward
  /// <paramref name="ambientC"/>. <paramref name="ratePerSecond"/> is the fraction of the remaining excess
  /// shed each second. Applied across a pass, it raises flow stress as the piece sits, so a load the run
  /// carried at the start can grow past what it can carry.
  /// </summary>
  public static float Cool(
    float tempC,
    float ambientC,
    float ratePerSecond,
    float dt
  ) {
    if (dt <= 0f || ratePerSecond <= 0f || tempC <= ambientC)
      return tempC;
    float retained = MathF.Exp(-ratePerSecond * dt);
    return ambientC + (tempC - ambientC) * retained;
  }
}
