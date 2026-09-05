using System;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// One melting hole of a crucible hearth: the pot standing in it, what is in the pot, and how far through
/// its heat it is. Pure and world-free, so the whole rhythm - seat, charge, preheat, melt, pull - is
/// testable without a furnace round it. See docs/design/machines/crucible-furnace.md.
/// </summary>
/// <remarks>
/// A mutable record of state rather than a value, because the hearth holds four of them across a save and
/// steps them in place. The rules that decide what happens to it live here; the block entity supplies the
/// clock, the world and the items.
/// </remarks>
public sealed class CrucibleHole {
  /// <summary>Whether a pot stands on this hole's clay stand.</summary>
  public bool Pot { get; private set; }

  /// <summary>Heats the seated pot has already given, carried so a pot pulled out is the pot put in.</summary>
  public int Firings { get; private set; }

  /// <summary>Blister-steel units in the pot, up to <see cref="IiexValues.CruciblePotChargeUnits"/>.</summary>
  public int Charge { get; private set; }

  /// <summary>Seconds of gentle heat the pot has taken.</summary>
  public float Preheat { get; private set; }

  /// <summary>Seconds of melting accrued once the pot is preheated and charged.</summary>
  public float Melt { get; private set; }

  /// <summary>Finished crucible-steel units standing in the pot, or zero before the melt completes.</summary>
  public int Metal { get; private set; }

  /// <summary>A pot with its full charge in it.</summary>
  public bool Charged => Charge >= IiexValues.CruciblePotChargeUnits;

  /// <summary>A pot that has come up gently and will no longer crack in the full fire.</summary>
  public bool Preheated => Preheat >= IiexValues.CruciblePreheatSec;

  /// <summary>A pot holding finished metal, ready to be pulled.</summary>
  public bool Molten => Metal > 0;

  /// <summary>Whether this hole has anything in it at all.</summary>
  public bool Occupied => Pot;

  /// <summary>How far through its melt the pot is, 0..1, for the readout.</summary>
  public float MeltFraction =>
    Math.Min(1f, Melt / Math.Max(1f, IiexValues.CrucibleMeltSec));

  /// <summary>How far through its preheat the pot is, 0..1, for the readout.</summary>
  public float PreheatFraction =>
    Math.Min(1f, Preheat / Math.Max(1f, IiexValues.CruciblePreheatSec));

  /// <summary>
  /// Stands a pot of <paramref name="firings"/> heats on the stand. Refuses a hole that already holds one,
  /// so a second pot is never swallowed.
  /// </summary>
  public bool Seat(int firings) {
    if (Pot)
      return false;
    Pot = true;
    Firings = Math.Max(0, firings);
    return true;
  }

  /// <summary>
  /// Takes <paramref name="offered"/> units of blister steel into the pot, whole or not at all, and
  /// returns how many were taken. Nothing goes into an empty hole, and nothing goes into a pot that has
  /// already melted.
  /// </summary>
  /// <remarks>
  /// Whole, because the caller is spending an item: a pot ten units short that swallowed a twenty-five
  /// unit chunk would eat the difference, and the whole chain from the helve down is built on the metal
  /// coming out equal to the metal going in. A pot short of a chunk takes bits instead, which is why the
  /// charge is four chunks and two bits rather than a round number of one of them.
  /// </remarks>
  public int TakeCharge(int offered) {
    if (
      !Pot
      || Molten
      || offered <= 0
      || Charge + offered > IiexValues.CruciblePotChargeUnits
    )
      return 0;
    Charge += offered;
    return offered;
  }

  /// <summary>
  /// One tick of gentle heat, with the damper shut. Only a pot that has not yet come up takes it; a
  /// preheated one is simply left alone.
  /// </summary>
  public void SoakTick(float dt) {
    if (Pot && !Preheated && dt > 0f)
      Preheat += dt;
  }

  /// <summary>
  /// Whether the full fire would destroy the pot standing here: one that is in the middle of coming up.
  /// An empty hole and a pot that has finished its preheat are both safe.
  /// </summary>
  /// <remarks>
  /// Deterministic rather than a chance. The player controls the one input that decides it - the damper -
  /// so a coin toss would make a correctly-run furnace lose pots anyway, and an incorrectly-run one
  /// sometimes get away with it. Neither teaches the rhythm.
  /// </remarks>
  public bool CracksInFullDraught => Pot && !Preheated;

  /// <summary>
  /// Advances the melt by <paramref name="seconds"/> and reports whether the pot finished on this step.
  /// A pot that is not charged, not preheated or already molten does not move.
  /// </summary>
  public bool MeltStep(float seconds) {
    if (!Pot || Molten || !Charged || !Preheated || seconds <= 0f)
      return false;

    Melt += seconds;
    if (Melt < IiexValues.CrucibleMeltSec)
      return false;

    Melt = 0f;
    Charge = 0;
    Metal = IiexValues.CruciblePotYieldUnits;
    return true;
  }

  /// <summary>
  /// Breaks the pot and returns the blister-steel units it was holding, which the hearth spills into the
  /// ash pit. The metal survives; the pot does not.
  /// </summary>
  /// <remarks>
  /// Blister, never finished steel: the only thing that cracks a pot is the full fire taken before the
  /// preheat is done (<see cref="CracksInFullDraught"/>), and a pot cannot have melted by then. Returning
  /// <see cref="Metal"/> as well would be spilling a case that cannot happen, in the wrong material.
  /// </remarks>
  public int Crack() {
    int spilled = Charge;
    Clear();
    return spilled;
  }

  /// <summary>Empties the hole completely.</summary>
  public void Clear() {
    Pot = false;
    Firings = 0;
    Charge = 0;
    Preheat = 0f;
    Melt = 0f;
    Metal = 0;
  }

  /// <summary>Restores a hole from stored values, for the block entity's own deserialization.</summary>
  public void Restore(
    bool pot,
    int firings,
    int charge,
    float preheat,
    float melt,
    int metal
  ) {
    Pot = pot;
    Firings = Math.Max(0, firings);
    Charge = Math.Max(0, charge);
    Preheat = Math.Max(0f, preheat);
    Melt = Math.Max(0f, melt);
    Metal = Math.Max(0, metal);
  }
}
