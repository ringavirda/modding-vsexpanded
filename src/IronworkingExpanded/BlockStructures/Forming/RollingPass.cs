using System;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The physics of one pass through a two-high rolling stand — pure, so the whole model is pinned headless
/// (the visible mill is just a readout of it). This is the <b>first real load</b> on the mechanical-energy
/// network: everything above it (the flywheel, the shaft run, the transmissions) exists to deliver the torque
/// this class demands. See <c>docs/design/iwex.md § Forming</c> and <c>docs/design/mp-energy-network.md §4.7</c>.
/// <para>
/// Three textbook relations carry the whole design, and each one lands on a gameplay rule the design already
/// wanted:
/// </para>
/// <list type="bullet">
///   <item><b>Bite:</b> <c>δ_max = μ²R</c>. Friction alone drags the stock in, so the reduction a stand can
///   take is capped by the friction coefficient squared. Hot iron grips (μ≈0.5), cold iron slips (μ≈0.09) —
///   about <b>30× the draft per pass</b> hot. This is why the mill is hot-work-only and why the gap sequence
///   is cut into the barrel: each segment is one legal step, and you cannot skip one.</item>
///   <item><b>Force:</b> <c>F = Y·w·L_c</c> over the arc of contact <c>L_c = √(R·δ)</c>. Force grows with the
///   <em>square root</em> of the draft, so a deep pass is not catastrophically harder — but it grows linearly
///   with flow stress, which is where temperature bites.</item>
///   <item><b>Torque:</b> <c>T = 2·F·a</c>, lever arm <c>a ≈ L_c/2</c>, two rolls. This is the number the
///   network sees, and it is what stalls the run when the stock is too cold or the gap too tight.</item>
/// </list>
/// <para>
/// The payoff is that "keep the stock hot or the mill jams" is not a special case — it falls out of the flow
/// stress term, exactly as <c>mp-energy-network.md §4.7</c> asks.
/// </para>
/// </summary>
public static class RollingPass
{
  /// <summary>Friction coefficient of <b>hot</b> iron against a cast roll — it grips, so it bites deep.</summary>
  public const float HotFriction = 0.5f;

  /// <summary>Friction coefficient of <b>cold</b> iron — it slips, so a cold stand can barely take a draft
  /// (μ² makes this ~30× worse than hot, which is the whole reason this mill is a hot mill).</summary>
  public const float ColdFriction = 0.09f;

  /// <summary>
  /// The largest draft (thickness reduction) a stand of roll radius <paramref name="rollRadius"/> can pull in
  /// by friction alone: <c>δ_max = μ²R</c>. Above this the stock simply will not enter — the rolls skid on it.
  /// </summary>
  public static float MaxDraft(float rollRadius, float friction) =>
    rollRadius <= 0f || friction <= 0f ? 0f : friction * friction * rollRadius;

  /// <summary>
  /// Whether the rolls can <b>bite</b> a piece at <paramref name="tempC"/> for the requested
  /// <paramref name="draft"/>. Below <paramref name="rollingTempC"/> the stock is treated as cold and its
  /// friction collapses, so a draft that a hot piece takes easily is refused outright — the player is told to
  /// reheat rather than being allowed to stall the whole run.
  /// </summary>
  public static bool CanBite(
    float draft,
    float rollRadius,
    float tempC,
    float rollingTempC
  ) => draft > 0f && draft <= MaxDraft(rollRadius, FrictionAt(tempC, rollingTempC));

  /// <summary>Friction at a given stock temperature: hot above the rolling heat, cold below it. Modelled as a
  /// step rather than a ramp because the underlying change (the oxide/scale film and recrystallisation) really
  /// is abrupt, and a hard line gives the player an unambiguous rule.</summary>
  public static float FrictionAt(float tempC, float rollingTempC) =>
    tempC >= rollingTempC ? HotFriction : ColdFriction;

  /// <summary>
  /// Average flow stress (relative units) of the stock at <paramref name="tempC"/>. At and above the rolling
  /// heat it sits at its hot floor of <c>1</c>; below, it climbs steeply toward
  /// <paramref name="coldMultiplier"/> as the piece loses heat, reaching the full multiplier once the stock is
  /// <paramref name="coldSpanC"/> degrees under. That climb is what turns a cooling piece into a stalling load.
  /// </summary>
  public static float FlowStress(
    float tempC,
    float rollingTempC,
    float coldMultiplier,
    float coldSpanC
  )
  {
    if (tempC >= rollingTempC)
      return 1f;
    if (coldSpanC <= 0f)
      return coldMultiplier;
    float coldness = Math.Clamp((rollingTempC - tempC) / coldSpanC, 0f, 1f);
    return 1f + (MathF.Max(1f, coldMultiplier) - 1f) * coldness;
  }

  /// <summary>The arc of contact <c>L_c = √(R·δ)</c> — how much of the roll is actually pressing on the stock.
  /// Both the force and the lever arm derive from it.</summary>
  public static float ContactLength(float rollRadius, float draft) =>
    rollRadius <= 0f || draft <= 0f ? 0f : MathF.Sqrt(rollRadius * draft);

  /// <summary>
  /// The resisting torque (N·m) a pass imposes on the run: <c>T = 2·F·a</c> with <c>F = Y·w·L_c</c> and lever
  /// arm <c>a = L_c/2</c>, which reduces to <c>T = Y·w·R·δ·k</c>. Scaled by <paramref name="torqueScale"/> so
  /// the whole mill can be balanced against the flywheel from config without touching the physics.
  /// <para>
  /// Note it is <b>independent of shaft speed</b>: a rolling pass is a plastic-deformation load, so it resists
  /// the same however fast the rolls turn. That is exactly why it can stall a run — unlike friction, it does
  /// not ease off as ω falls.
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
  )
  {
    float contact = ContactLength(rollRadius, draft);
    if (contact <= 0f || width <= 0f)
      return 0f;
    float flowStress = FlowStress(tempC, rollingTempC, coldMultiplier, coldSpanC);
    // F = Y * w * L_c, and T = 2 * F * (L_c / 2) = F * L_c -> Y * w * L_c^2 = Y * w * R * delta.
    return flowStress * width * contact * contact * torqueScale;
  }

  /// <summary>
  /// Whether a run turning at <paramref name="speed"/> with <paramref name="availableTorque"/> on tap can carry
  /// <paramref name="loadTorque"/>. A stalled shaft (ω at rest) cannot start a pass at all, which is the
  /// design's "spin the flywheel up before you roll" startup ritual falling out of the model rather than being
  /// special-cased.
  /// </summary>
  public static bool CanCarry(float loadTorque, float availableTorque, float speed) =>
    speed > 0f && availableTorque >= loadTorque;

  /// <summary>
  /// How wide a piece becomes once it is rolled from <paramref name="startThickness"/> down to
  /// <paramref name="thickness"/>: <c>w = w₀·(t₀/t)^e</c>, never past <paramref name="maxWidth"/>.
  /// <para>
  /// Squeezed metal has to go somewhere, and in flat rolling it goes into <b>both</b> length and width - the
  /// lateral part is called <em>spread</em>. The exponent sets the split (length takes the complement,
  /// <c>1-e</c>, so volume is conserved), and it is per-form because <b>spread falls as stock gets wider
  /// relative to its thickness</b>: friction across a wide face resists sideways flow, while a narrow bar has
  /// nothing holding it in. See <see cref="StockForm"/>.
  /// </para>
  /// <para>
  /// Spread is what gives the barrel width a job: once a piece is wider than the rolls it no longer fits in
  /// one bite, so the mill takes it in side-by-side passes (<see cref="RollSetSpec.PassesAt"/>). A wide set
  /// sidesteps that entirely, which is exactly why plate mills used wide rolls. The
  /// <paramref name="maxWidth"/> ceiling is the piece finally running out of room between the housings.
  /// </para>
  /// </summary>
  public static float SpreadWidth(
    float startWidth,
    float startThickness,
    float thickness,
    float exponent = 0.5f,
    float maxWidth = 0f
  )
  {
    if (startWidth <= 0f)
      return 0f;
    if (startThickness <= 0f || thickness <= 0f)
      return startWidth;
    float width = startWidth * MathF.Pow(startThickness / thickness, exponent);
    return maxWidth > 0f ? MathF.Min(width, maxWidth) : width;
  }

  /// <summary>
  /// How much longer the piece gets, as a multiple of its starting length. Whatever the reduction does not put
  /// into width has to go into length, so this is derived from the <em>actual</em> (possibly capped) width
  /// rather than from the exponent - once a piece stops widening, every further reduction runs out lengthways.
  /// That is why a fully-rolled bloom ends up long out of proportion to how it started.
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
  /// The stock's temperature after <paramref name="dt"/> seconds, cooling exponentially toward
  /// <paramref name="ambientC"/> at <paramref name="ratePerSecond"/> (the fraction of the remaining excess it
  /// sheds each second).
  /// <para>
  /// This is what closes the keep-it-hot loop rather than leaving it a one-off check at the bite. A piece
  /// enters comfortably hot, and every second under the rolls it stiffens: the flow stress climbs, the load
  /// climbs with it, and a schedule the line could carry at the start can drag it to a stall by the last pass.
  /// It is also why a <b>jam is self-worsening</b> - a stalled piece keeps cooling, so the longer it sits the
  /// more torque it needs to move, and past the bite threshold the answer is the reheat furnace, not more power.
  /// </para>
  /// </summary>
  public static float Cool(float tempC, float ambientC, float ratePerSecond, float dt)
  {
    if (dt <= 0f || ratePerSecond <= 0f || tempC <= ambientC)
      return tempC;
    float retained = MathF.Exp(-ratePerSecond * dt);
    return ambientC + (tempC - ambientC) * retained;
  }
}
