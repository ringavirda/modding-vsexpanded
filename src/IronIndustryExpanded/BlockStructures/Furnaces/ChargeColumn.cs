using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IronIndustryExpanded.Items;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// One run of a single material in a shaft column - a band, as it reads on the furnace wall. Free-sized:
/// block boundaries quantise nothing, so a course is however much was loaded. The material is a code
/// string (<c>Code.ToShortString()</c>, e.g. <c>iiex:burden</c>, <c>game:coke</c>) rather than an
/// <see cref="Vintagestory.API.Common.ItemStack"/>, so segments persist cheaply, merge, and leave the
/// accepted set open. Burden's flux ratio rides along in <see cref="Mix"/> for the melt to read.
/// </summary>
/// <param name="Material">Item code of the charged material.</param>
/// <param name="Units">How much of it this band holds.</param>
/// <param name="Temperature">Temperature the band carries, in °C.</param>
/// <param name="Mix">Burden composition; <c>default</c> for fuel bands.</param>
public readonly record struct ChargeSegment(
  string Material,
  int Units,
  float Temperature,
  BurdenMix Mix
);

/// <summary>
/// A run of consecutive bands drawing the same material - one stripe on the shaft wall, and one quad
/// strip for whatever meshes it. Run-length rather than one entry per band, so a course straddling a
/// block boundary stays one stripe when both sides carry the same <see cref="Material"/> and
/// <see cref="Mix"/>.
/// </summary>
/// <param name="Material">Item code the run draws, as <see cref="ChargeSegment.Material"/>.</param>
/// <param name="Mix">Burden composition of the segment it came from; <c>default</c> for fuel.</param>
/// <param name="Bands">How many consecutive bands the run covers.</param>
public readonly record struct ChargeBandRun(
  string Material,
  BurdenMix Mix,
  int Bands
);

/// <summary>
/// The charge in one shaft column <c>(x, z)</c>: an ordered list of <see cref="ChargeSegment"/>s, raceway
/// end first. Index <c>0</c> is the bottom, where the tuyeres are and where consumption happens; pushes
/// land on top, and the empty space always appears there, so descent is one subtraction per column. Pure
/// data - no <c>Api</c>, no <c>BlockPos</c>, no world; the blocks over it are renderers. See
/// <c>docs/design/layered-charge.md</c>.
/// </summary>
public sealed class ChargeColumn {
  /// <summary>
  /// How far two temperatures may differ and still count as the same band, in °C. Not config: it absorbs
  /// float drift between loads that arrived at the same ambient, so consecutive hand-charged loads
  /// coalesce into one stripe. A wider value lets a warm descending segment swallow a cold new one,
  /// inventing heat the furnace never produced.
  /// </summary>
  public const float TempMergeEpsilon = 1f;

  /// <summary>
  /// Bands one charge block draws. Not config: a block is 16 voxels tall and the pile shape grows one
  /// voxel layer at a time, so any other value draws bands that do not land on voxels. The tunable is the
  /// furnace's <c>ChargeUnitsPerBlock</c>, a per-block quantum because a per-band integer cannot express
  /// the remelt pile's 3 000 u over 16 bands.
  /// </summary>
  public const int BandsPerBlock = 16;

  private const string MaterialsKey = "segMaterials";
  private const string UnitsKey = "segUnits";
  private const string TempsKey = "segTemps";
  private const string MixKey = "segMix";

  private readonly List<ChargeSegment> _segments = [];
  private readonly ReadOnlyCollection<ChargeSegment> _readOnlySegments;

  public ChargeColumn() {
    // Wrapped once rather than per read: the renderer walks this every frame.
    _readOnlySegments = _segments.AsReadOnly();
  }

  /// <summary>
  /// The bands, raceway end first. The charge-pile renderer walks these into its 16-band window. Genuinely
  /// read-only, so a caller cannot cast back to the backing list and reorder bands behind the column.
  /// </summary>
  public IReadOnlyList<ChargeSegment> Segments => _readOnlySegments;

  /// <summary>
  /// Everything the column holds. Summed on each read rather than cached: a column carries a handful of
  /// segments, and a running total could drift out of step with a split.
  /// </summary>
  public int TotalUnits {
    get {
      int total = 0;
      foreach (ChargeSegment segment in _segments)
        total += segment.Units;
      return total;
    }
  }

  /// <summary>What is lying on top, or null on an empty column. The band-order rule reads it to decide
  /// whether an incoming load starts a fresh course or continues the one being laid.</summary>
  public string? TopMaterial =>
    _segments.Count == 0 ? null : _segments[^1].Material;

  #region Charging and descent

  /// <summary>
  /// Lays <paramref name="units"/> of <paramref name="material"/> on top, merging into the top band when
  /// it is the same material, the same <paramref name="mix"/> and within <see cref="TempMergeEpsilon"/>;
  /// the merged temperature is the unit-weighted average of the two. A non-positive
  /// <paramref name="units"/> or a null or empty <paramref name="material"/> is a no-op, so an unresolved
  /// item code cannot put an unwritable segment in the column and fail inside the engine's chunk save.
  /// </summary>
  public void Push(string material, int units, float temperature, BurdenMix mix) {
    if (units <= 0 || string.IsNullOrEmpty(material))
      return;

    Append(_segments, new ChargeSegment(material, units, temperature, mix));
  }

  /// <summary>
  /// Removes <paramref name="units"/> from the bottom and returns what came off, raceway end first - the
  /// descent the raceway burns and melts. A take landing mid-band splits it and both halves keep the
  /// temperature, which makes the melt condition exact at unit granularity. Never returns more than the
  /// column holds; an empty column returns empty, a normal tick state rather than an error.
  /// </summary>
  public List<ChargeSegment> Take(int units) {
    List<ChargeSegment> taken = LowestUnits(units);

    int remaining = 0;
    foreach (ChargeSegment segment in taken)
      remaining += segment.Units;

    int spent = 0;
    while (spent < _segments.Count && remaining > 0) {
      ChargeSegment segment = _segments[spent];
      if (segment.Units > remaining) {
        // The boundary band survives, shorter and at the same temperature.
        _segments[spent] = segment with {
          Units = segment.Units - remaining,
        };
        remaining = 0;
      } else {
        remaining -= segment.Units;
        spent++;
      }
    }

    if (spent > 0)
      _segments.RemoveRange(0, spent);
    return taken;
  }

  /// <summary>
  /// Removes <paramref name="units"/> from the top and returns what came off, stockline end first - the
  /// inverse of <see cref="Push"/>, and the only thing a player at the shaft can do to a column by hand.
  /// Clamping and the mid-band split match <see cref="Take"/>: never more than the column holds, an empty
  /// column returns empty, and both halves of a split keep the temperature.
  /// </summary>
  public List<ChargeSegment> TakeTop(int units) {
    List<ChargeSegment> taken = HighestUnits(units);

    int remaining = 0;
    foreach (ChargeSegment segment in taken)
      remaining += segment.Units;

    int spent = 0;
    while (spent < _segments.Count && remaining > 0) {
      int at = _segments.Count - 1 - spent;
      ChargeSegment segment = _segments[at];
      if (segment.Units > remaining) {
        // The boundary band survives, shorter and at the same temperature.
        _segments[at] = segment with {
          Units = segment.Units - remaining,
        };
        remaining = 0;
      } else {
        remaining -= segment.Units;
        spent++;
      }
    }

    if (spent > 0)
      _segments.RemoveRange(_segments.Count - spent, spent);
    return taken;
  }

  /// <summary>
  /// Removes the <paramref name="units"/> units beginning <paramref name="fromUnit"/> units above the
  /// raceway end, returned raceway-first - what breaking a charge pile out of the shaft wall does. What
  /// is above the gap falls, laid back through <see cref="Append"/> so the two sides merge if they meet
  /// as the same band. Clamped like <see cref="Take"/>; a span past the top, a non-positive count and an
  /// empty column all return empty. See <c>docs/design/layered-charge.md</c>.
  /// </summary>
  public List<ChargeSegment> TakeSpan(int fromUnit, int units) {
    var taken = new List<ChargeSegment>();
    if (units <= 0 || _segments.Count == 0)
      return taken;

    int from = Math.Max(0, fromUnit);
    int to = from + units; // exclusive

    var kept = new List<ChargeSegment>(_segments.Count + 2);
    int at = 0; // unit offset of the current band's raceway-end edge

    foreach (ChargeSegment segment in _segments) {
      int start = at;
      int end = at + segment.Units;
      at = end;

      if (end <= from || start >= to) {
        Append(kept, segment);
        continue;
      }

      // The part below the cut survives where it is.
      if (start < from)
        Append(kept, segment with { Units = from - start });

      int cutFrom = Math.Max(start, from);
      int cutTo = Math.Min(end, to);
      Append(taken, segment with { Units = cutTo - cutFrom });

      // ...and the part above it falls onto whatever is now beneath.
      if (end > to)
        Append(kept, segment with { Units = end - to });
    }

    _segments.Clear();
    _segments.AddRange(kept);
    return taken;
  }

  /// <summary>
  /// The highest <paramref name="units"/> the column holds, stockline end first, without removing them, so
  /// an interaction can see which material and mix it is about to be handed. Mirror of
  /// <see cref="LowestUnits"/>, with the same clamping.
  /// </summary>
  public List<ChargeSegment> HighestUnits(int units) {
    var highest = new List<ChargeSegment>();
    int remaining = units;
    for (int i = _segments.Count - 1; i >= 0 && remaining > 0; i--) {
      ChargeSegment segment = _segments[i];
      int part = Math.Min(segment.Units, remaining);
      highest.Add(segment with { Units = part });
      remaining -= part;
    }
    return highest;
  }

  /// <summary>
  /// The lowest <paramref name="units"/> the column holds, raceway end first, without removing them, so the
  /// melt condition can be evaluated against exactly what a <see cref="Take"/> would yield. Same clamping
  /// as <see cref="Take"/>: never more than is there, empty for an empty column or a non-positive request.
  /// </summary>
  public List<ChargeSegment> LowestUnits(int units) {
    var lowest = new List<ChargeSegment>();
    int remaining = units;
    for (int i = 0; i < _segments.Count && remaining > 0; i++) {
      ChargeSegment segment = _segments[i];
      int part = Math.Min(segment.Units, remaining);
      lowest.Add(segment with { Units = part });
      remaining -= part;
    }
    return lowest;
  }

  /// <summary>
  /// Rewrites the column in place one block at a time: every segment is split where it crosses a block
  /// boundary, each piece is passed to <paramref name="rewrite"/> with the index of the block it falls in,
  /// and what comes back is laid down in its place. Take-then-push cannot express this - <see cref="Push"/>'s
  /// merge rule would collapse the rewritten bands back into one stripe and lose the gradient burn-out puts
  /// in them. A piece rewritten to <c>Units &lt;= 0</c> is dropped, which is how a consumer burns fuel away;
  /// unchanged pieces re-merge, so a rewrite that changes nothing leaves the segment list as it found it.
  /// </summary>
  /// <param name="unitsPerBlock">The block quantum to split on - the furnace's
  /// <c>ChargeUnitsPerBlock</c>. Clamped to at least 1.</param>
  /// <param name="rewrite">Given a piece and the index of the block it sits in (0 at the column's floor),
  /// returns what should stand there instead.</param>
  public void Rewrite(
    int unitsPerBlock,
    Func<ChargeSegment, int, ChargeSegment> rewrite
  ) {
    if (_segments.Count == 0)
      return;

    int perBlock = Math.Max(1, unitsPerBlock);
    var rewritten = new List<ChargeSegment>(_segments.Count);
    int offset = 0;

    foreach (ChargeSegment segment in _segments) {
      int remaining = segment.Units;
      while (remaining > 0) {
        int blockIndex = offset / perBlock;
        // How much of this segment is still inside the block the offset currently sits in.
        int room = ((blockIndex + 1) * perBlock) - offset;
        int part = Math.Min(remaining, room);

        ChargeSegment piece = rewrite(
          segment with {
            Units = part,
          },
          blockIndex
        );
        if (piece.Units > 0 && !string.IsNullOrEmpty(piece.Material))
          Append(rewritten, piece);

        offset += part;
        remaining -= part;
      }
    }

    _segments.Clear();
    _segments.AddRange(rewritten);
  }

  /// <summary>
  /// Rewrites the lowest <paramref name="units"/> in place - the raceway's own span - splitting the band
  /// the boundary falls in and leaving the rest untouched, which is how coke stamped into a burden band
  /// burns out of the stamp while the ore stays put. Takes no block quantum, unlike <see cref="Rewrite"/>:
  /// what the raceway reaches is a depth in units. Pieces rewritten to <c>Units &lt;= 0</c> are dropped;
  /// unchanged pieces re-merge under <see cref="Push"/>'s rule.
  /// </summary>
  public void RewriteLowest(
    int units,
    Func<ChargeSegment, ChargeSegment> rewrite
  ) {
    if (_segments.Count == 0 || units <= 0)
      return;

    var rewritten = new List<ChargeSegment>(_segments.Count);
    int left = units;

    foreach (ChargeSegment segment in _segments) {
      if (left <= 0) {
        Append(rewritten, segment);
        continue;
      }

      int inside = Math.Min(segment.Units, left);
      left -= inside;

      ChargeSegment piece = rewrite(segment with { Units = inside });
      if (piece.Units > 0 && !string.IsNullOrEmpty(piece.Material))
        Append(rewritten, piece);

      // The far side of the boundary band is untouched - the raceway never reached it.
      if (inside < segment.Units)
        Append(rewritten, segment with { Units = segment.Units - inside });
    }

    _segments.Clear();
    _segments.AddRange(rewritten);
  }

  /// <summary>The merge half of <see cref="Push"/>, shared so every path that lays segments down uses one
  /// rule.</summary>
  private static void Append(List<ChargeSegment> into, ChargeSegment segment) {
    if (into.Count > 0) {
      ChargeSegment top = into[^1];
      if (
        top.Material == segment.Material
        && top.Mix == segment.Mix
        && Math.Abs(top.Temperature - segment.Temperature) <= TempMergeEpsilon
      ) {
        int merged = top.Units + segment.Units;
        into[^1] = top with {
          Units = merged,
          Temperature =
            (
              (top.Temperature * top.Units)
              + (segment.Temperature * segment.Units)
            ) / merged,
        };
        return;
      }
    }

    into.Add(segment);
  }

  #endregion

  #region Counter-current

  // The furnace is counter-current: all the coke burns at the raceway (index 0) and nowhere else, and the
  // gas that leaves it rises through everything above, warming it. A band is warmed by coke that burned
  // beneath it while it descended, never by spending its own, and what it carries at the raceway decides
  // whether it melts. Both operations here are pure: what varies per tick arrives as arguments.

  /// <summary>
  /// Passes hot gas up from the raceway end, warming every band; returns the gas temperature at the top,
  /// in °C. Absorption compounds per unit (<c>1 - (1-transferFraction)^U</c> per piece of <c>U</c> units),
  /// so the result is independent of the segment cuts, and each rise is clamped to the bodies' equilibrium.
  /// An empty column or a non-finite or non-positive argument returns <paramref name="gasTemp"/>.
  /// </summary>
  /// <param name="gasTemp">Flame temperature leaving the raceway, in °C.</param>
  /// <param name="gasHeatCapacityUnits">Charge units this step's gas could warm by one degree for each
  /// degree it falls. Proportional to the coke burned in the step, so it carries dt.</param>
  /// <param name="transferFraction">Share of the gas's remaining excess one unit of charge absorbs.</param>
  /// <param name="resolutionUnits">Tallest piece of charge allowed to be one temperature. Load-bearing: a
  /// column charged in one go is one band, warms uniformly and would show no profile at all.</param>
  public float RiseGasThrough(
    float gasTemp,
    float gasHeatCapacityUnits,
    float transferFraction,
    int resolutionUnits
  ) {
    // Written as `!(x > 0)` rather than `x <= 0` so a NaN argument is rejected too, not admitted.
    if (
      _segments.Count == 0
      || !float.IsFinite(gasTemp)
      || !(gasHeatCapacityUnits > 0f)
      || !(transferFraction > 0f)
    )
      return gasTemp;

    float capacity = gasHeatCapacityUnits;
    float retained = 1f - Math.Min(1f, transferFraction);
    int resolution = Math.Max(1, resolutionUnits);
    float gas = gasTemp;

    var warmed = new List<ChargeSegment>(_segments.Count);
    foreach (ChargeSegment segment in _segments) {
      int left = segment.Units;
      float start = segment.Temperature;
      while (left > 0) {
        int piece = Math.Min(left, resolution);
        left -= piece;

        float rise = 0f;
        if (gas > start) {
          float absorbed = 1f - MathF.Pow(retained, piece);
          float equilibrium =
            ((capacity * gas) + (piece * start)) / (capacity + piece);
          rise = Math.Max(
            0f,
            Math.Min(
              capacity * (gas - start) * absorbed / piece,
              equilibrium - start
            )
          );
        }

        // Laid down through Push's merge rule, so pieces the gas left identical fold back together.
        Append(
          warmed,
          segment with {
            Units = piece,
            Temperature = start + rise,
          }
        );
        gas -= rise * piece / capacity;
      }
    }

    _segments.Clear();
    _segments.AddRange(warmed);
    return gas;
  }

  /// <summary>
  /// The hottest band overlapping units <c>[fromUnit, fromUnit + units)</c>, in °C, or <c>0</c> where the
  /// span holds nothing. A span off either end reports whatever part of it is real. Hottest rather than
  /// average: a block carries one light value over up to sixteen bands, and one white-hot band should
  /// light it.
  /// </summary>
  public float PeakTemperature(int fromUnit, int units) {
    if (units <= 0)
      return 0f;

    int from = Math.Max(0, fromUnit);
    long to = (long)from + units;
    float peak = 0f;
    bool any = false;
    int at = 0;

    foreach (ChargeSegment segment in _segments) {
      int end = at + segment.Units;
      if (end > from && at < to) {
        if (!any || segment.Temperature > peak)
          peak = segment.Temperature;
        any = true;
      }
      at = end;
      if (at >= to)
        break;
    }

    return any ? peak : 0f;
  }

  #endregion

  #region Materialisation

  // What the blocks over the column draw. Pure functions of the segment list plus a band size, which
  // arrives as a parameter rather than being read off IiexValues, so both the renderer and the block
  // placer can run with no scene and at any band size.

  /// <summary>
  /// How many charge blocks tall a column of <paramref name="units"/> units stands, at
  /// <paramref name="unitsPerBlock"/> units per block, rounded up so a part-filled block still gets a
  /// block to draw it. <c>SyncChargeBlocks</c> places and removes <c>iiex:furnace-chargepile</c> as this
  /// crosses a boundary.
  /// </summary>
  public static int BlocksTall(int units, int unitsPerBlock) {
    if (units <= 0)
      return 0;
    long perBlock = Math.Max(1, unitsPerBlock);
    return (int)(((units - 1) / perBlock) + 1);
  }

  /// <summary>
  /// What the charge block at height index <paramref name="blockIndex"/> draws: bands
  /// <c>blockIndex·16 … (blockIndex+1)·16</c> of the column, raceway end first, run-length encoded. Bands
  /// do not snap to block boundaries, so a course taller than a block runs on across the next as one
  /// stripe; a band straddling two segments draws the one filling most of it, ties going to the
  /// raceway-nearer one. Returns fewer than <see cref="BandsPerBlock"/> bands' worth, possibly none, where
  /// the column ends inside the block, and empty for any <paramref name="blockIndex"/> outside the column
  /// at any magnitude.
  /// </summary>
  public List<ChargeBandRun> BandsAt(int blockIndex, int unitsPerBlock) {
    var runs = new List<ChargeBandRun>();
    if (blockIndex < 0)
      return runs;

    long perBlock = Math.Max(1, unitsPerBlock);
    int total = TotalUnits;

    // Walks with the bands rather than restarting per band: bands ascend, so a segment already left
    // behind can never be wanted again.
    int seg = 0;
    int segBase = 0;

    for (int i = 0; i < BandsPerBlock; i++) {
      // Widened to long: in int this multiply wraps at blockIndex 16 777 216 (at 8 u a band) and lands
      // negative, which slips past the `low >= total` break and draws a phantom block, so with the
      // widening the lower guard needs no upper twin. Band boundaries derive from the block quantum
      // rather than a per-band one - the remelt pile's 3 000 u/block over 16 bands is 187.5 u a band,
      // which a per-band integer cannot express - so multiplying first and dividing after keeps it exact.
      long band = ((long)blockIndex * BandsPerBlock) + i;
      long lowUnits = (band * perBlock) / BandsPerBlock;
      if (lowUnits >= total)
        break;
      long highUnits = ((band + 1) * perBlock) / BandsPerBlock;
      // Both narrowings are safe: 0 <= lowUnits < total <= int.MaxValue, and high is clamped to total.
      int low = (int)lowUnits;
      int high = (int)Math.Min(highUnits, total);

      while (seg < _segments.Count && segBase + _segments[seg].Units <= low) {
        segBase += _segments[seg].Units;
        seg++;
      }
      if (seg >= _segments.Count)
        break;

      string material = _segments[seg].Material;
      BurdenMix mix = _segments[seg].Mix;
      int best = 0;
      for (int s = seg, at = segBase; s < _segments.Count && at < high; s++) {
        ChargeSegment segment = _segments[s];
        int end = at + segment.Units;
        int overlap = Math.Min(end, high) - Math.Max(at, low);
        // Strictly greater, so an exact tie keeps the lower segment and the rule stays deterministic.
        if (overlap > best) {
          best = overlap;
          material = segment.Material;
          mix = segment.Mix;
        }
        at = end;
      }

      if (
        runs.Count > 0
        && runs[^1].Material == material
        && runs[^1].Mix == mix
      )
        runs[^1] = runs[^1] with { Bands = runs[^1].Bands + 1 };
      else
        runs.Add(new ChargeBandRun(material, mix, 1));
    }

    return runs;
  }

  #endregion

  #region Serialization

  /// <summary>
  /// Writes the column into <paramref name="tree"/> as four parallel arrays. Always writes, including for
  /// an empty column, so a column drawn down to nothing does not reload with the previous save's contents.
  /// </summary>
  public void ToTree(ITreeAttribute tree) {
    int count = _segments.Count;
    var materials = new string[count];
    var units = new int[count];
    var temperatures = new float[count];
    var mix = new float[count * 3];

    for (int i = 0; i < count; i++) {
      ChargeSegment segment = _segments[i];
      materials[i] = segment.Material;
      units[i] = segment.Units;
      temperatures[i] = segment.Temperature;
      mix[(i * 3) + 0] = segment.Mix.Iron;
      mix[(i * 3) + 1] = segment.Mix.Flux;
      mix[(i * 3) + 2] = segment.Mix.Fuel;
    }

    tree[MaterialsKey] = new StringArrayAttribute(materials);
    tree[UnitsKey] = new IntArrayAttribute(units);
    tree[TempsKey] = new FloatArrayAttribute(temperatures);
    tree[MixKey] = new FloatArrayAttribute(mix);
  }

  /// <summary>
  /// Replaces the column with what <paramref name="tree"/> holds; a tree carrying no column state leaves it
  /// empty. Reads only as far as the arrays agree, so a truncated or half-written save loses the tail rather
  /// than throwing on load, and drops any band the save cannot describe.
  /// </summary>
  public void FromTree(ITreeAttribute tree) {
    _segments.Clear();

    int[] units = (tree[UnitsKey] as IntArrayAttribute)?.value ?? [];
    string[] materials =
      (tree[MaterialsKey] as StringArrayAttribute)?.value ?? [];
    float[] temperatures = (tree[TempsKey] as FloatArrayAttribute)?.value ?? [];
    float[] mix = (tree[MixKey] as FloatArrayAttribute)?.value ?? [];

    int count = Math.Min(units.Length, materials.Length);
    for (int i = 0; i < count; i++) {
      // A truncated or version-skewed save can carry a band no push could have made: a negative count
      // runs Take's clamp backwards, and merging onto a zero-unit band divides by zero for a NaN
      // temperature that then poisons every later comparison.
      if (units[i] <= 0 || string.IsNullOrEmpty(materials[i]))
        continue;

      _segments.Add(
        new ChargeSegment(
          materials[i],
          units[i],
          i < temperatures.Length ? temperatures[i] : 0f,
          (i * 3) + 2 < mix.Length
            ? new BurdenMix(mix[i * 3], mix[(i * 3) + 1], mix[(i * 3) + 2])
            : default
        )
      );
    }
  }

  #endregion
}
