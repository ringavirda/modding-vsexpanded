using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IronworkingExpanded.Items;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// One run of a single material in a shaft column - a band, as it reads on the furnace wall. Free-sized:
/// block boundaries quantise nothing, so a course is simply however much the player loaded.
/// <para>
/// <b>Why the mix rides along.</b> With coke charged separately, burden's one surviving quality is its flux
/// ratio, and it has to survive descent so the melt can read the mix that actually arrives at the raceway.
/// Coke segments carry <c>default</c>.
/// </para>
/// <para>
/// <b>Why the material is a code string</b> (<c>Code.ToShortString()</c> - <c>iwex:burden</c>,
/// <c>game:coke</c>) rather than an <see cref="Vintagestory.API.Common.ItemStack"/> or an enum: a stack per
/// segment would be heavy to persist and impossible to merge, and an enum would close the set the cupola
/// accepts.
/// </para>
/// </summary>
/// <param name="Material">Item code of the charged material.</param>
/// <param name="Units">How much of it this band holds.</param>
/// <param name="Temperature">What the band carries, in °C - warmed by coke that burned beneath it while it
/// descended, not by spending its own.</param>
/// <param name="Mix">Burden composition; <c>default</c> for fuel bands.</param>
public readonly record struct ChargeSegment(
  string Material,
  int Units,
  float Temperature,
  BurdenMix Mix
);

/// <summary>
/// A run of consecutive bands drawing the same material - one stripe on the shaft wall, and one quad
/// strip for whatever meshes it.
/// <para>
/// <b>Why run-length rather than 16 entries per block.</b> A charged furnace is a handful of thick
/// courses, not sixteen alternating ones, so a block is typically one to three runs; the mesher wants the
/// stripe, not the sixteen slices it would have to re-merge. It also makes "a course straddling a block
/// boundary is one continuous stripe" a fact the caller can read directly - the last run of one block and
/// the first run of the next carry the same <see cref="Material"/> and <see cref="Mix"/>.
/// </para>
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
/// end first. Index <c>0</c> is the <b>bottom</b> - where the tuyeres are and where consumption happens -
/// and pushes land on top:
/// <code>
/// [ {coke, 3 u, 1180 °C}, {burden, 9 u, 1140 °C}, {coke, 3 u, 980 °C}, {burden, 11 u, 940 °C}, … ]
///   ↑ index 0, raceway end                                              ↑ the hopper pushes here
/// </code>
/// <para>
/// <b>Why this is not blocks.</b> Vanilla's coal pile collapses its own column from inside its block entity
/// (<c>TriggerPileChanged → TryPartialCollapse</c>, both private, reached from interaction and merge), which
/// is what forced consumption to be written top-down and what made per-layer temperature untenable. Holding
/// the charge here means descent is one subtraction per column: nothing falls, nothing collapses, and the
/// empty space always appears at the top - which is where the hopper already drips.
/// </para>
/// <para>
/// Pure data: no <c>Api</c>, no <c>BlockPos</c>, no world. The blocks over it are renderers.
/// </para>
/// </summary>
public sealed class ChargeColumn
{
  /// <summary>
  /// How far two temperatures may differ and still count as the same band, in °C.
  /// <para>
  /// Deliberately <b>not</b> config. It is a model invariant, not a tuning knob: it exists only to absorb
  /// float drift between two loads that arrived at the same ambient, so that consecutive hand-charged loads
  /// coalesce into one visible stripe. Widening it would let a warm descending segment swallow a cold new
  /// one, which invents heat the furnace never produced.
  /// </para>
  /// </summary>
  public const float TempMergeEpsilon = 1f;

  /// <summary>
  /// Bands one charge block draws.
  /// <para>
  /// Deliberately <b>not</b> config, for the same reason as <see cref="TempMergeEpsilon"/>: it is the
  /// block's own vertical resolution, not a tuning knob. A block is 16 voxels tall and the pile shape
  /// grows one voxel layer at a time, so any other value would draw bands that do not land on voxels.
  /// The knob is the furnace's own <c>ChargeUnitsPerBlock</c> - how much charge a block holds - which is
  /// what actually changes how tall a given charge stands. It was <c>ChargeUnitsPerBand</c> until the
  /// remelt pile forced the quantum per block: 3 000 u over 16 bands is 187.5, which no integer per-band
  /// constant can express.
  /// </para>
  /// </summary>
  public const int BandsPerBlock = 16;

  private const string MaterialsKey = "segMaterials";
  private const string UnitsKey = "segUnits";
  private const string TempsKey = "segTemps";
  private const string MixKey = "segMix";

  private readonly List<ChargeSegment> _segments = [];
  private readonly ReadOnlyCollection<ChargeSegment> _readOnlySegments;

  public ChargeColumn()
  {
    // Wrapped once rather than per read: the renderer walks this every frame.
    _readOnlySegments = _segments.AsReadOnly();
  }

  /// <summary>
  /// The bands, raceway end first. The charge-pile renderer walks these into its 16-band window.
  /// <para>
  /// Genuinely read-only, not the backing list behind an interface - a caller who casts back out could
  /// insert or reorder bands behind the column's back, and warming a descending band is exactly what a
  /// later phase will want, so the door is shut before someone finds the cast instead of an API.
  /// </para>
  /// </summary>
  public IReadOnlyList<ChargeSegment> Segments => _readOnlySegments;

  /// <summary>
  /// Everything the column holds. Summed rather than cached - a column carries a handful of segments, so
  /// there is nothing to win and a running total is one more thing that can drift out of step with a split.
  /// </summary>
  public int TotalUnits
  {
    get
    {
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
  /// Lays <paramref name="units"/> of <paramref name="material"/> on top of the column.
  /// <para>
  /// Merges into the current top band when it is the same material, the same <paramref name="mix"/> and
  /// within <see cref="TempMergeEpsilon"/>; the merged temperature is the <b>unit-weighted</b> average of
  /// the two, which is what keeps a course laid in several handfuls a single stripe without inventing heat.
  /// </para>
  /// <para>
  /// A non-positive <paramref name="units"/> is a no-op. The hopper drips every tick and an empty drip is
  /// normal, not exceptional.
  /// </para>
  /// <para>
  /// So is a missing <paramref name="material"/>, for the same reason it is not a throw: the hopper reads
  /// its code off a stack (<c>Collectible?.Code?.ToShortString()</c>), which comes back null for anything
  /// a removed or renamed mod left unresolved - a live world state, not an authoring mistake, on a path
  /// that runs every tick. Refusing it here keeps the failure local; accepting it puts an unwritable
  /// segment in the column and surfaces the throw much later, inside the engine's chunk save.
  /// </para>
  /// </summary>
  public void Push(
    string material,
    int units,
    float temperature,
    BurdenMix mix
  )
  {
    if (units <= 0 || string.IsNullOrEmpty(material))
      return;

    Append(_segments, new ChargeSegment(material, units, temperature, mix));
  }

  /// <summary>
  /// Removes <paramref name="units"/> from the <b>bottom</b> and returns what came off, raceway end first -
  /// descent, and what the raceway actually gets to burn and melt.
  /// <para>
  /// A take that lands mid-band splits it, and <b>both halves keep the temperature</b>: heat is a property
  /// of the material, not of the quantity, so splitting it proportionally would be wrong. That is what makes
  /// the melt condition exact at unit granularity rather than per round.
  /// </para>
  /// <para>
  /// Never returns more than the column holds, and an empty column returns empty. Descent runs every tick
  /// and a starved column is a normal state, not an error.
  /// </para>
  /// </summary>
  public List<ChargeSegment> Take(int units)
  {
    List<ChargeSegment> taken = LowestUnits(units);

    int remaining = 0;
    foreach (ChargeSegment segment in taken)
      remaining += segment.Units;

    int spent = 0;
    while (spent < _segments.Count && remaining > 0)
    {
      ChargeSegment segment = _segments[spent];
      if (segment.Units > remaining)
      {
        // The boundary band survives, shorter and at the same temperature.
        _segments[spent] = segment with { Units = segment.Units - remaining };
        remaining = 0;
      }
      else
      {
        remaining -= segment.Units;
        spent++;
      }
    }

    if (spent > 0)
      _segments.RemoveRange(0, spent);
    return taken;
  }

  /// <summary>
  /// Removes <paramref name="units"/> from the <b>top</b> and returns what came off, stockline end first -
  /// the exact mirror of <see cref="Take"/>, and the only thing a player standing at the shaft can do to a
  /// column by hand.
  /// <para>
  /// <b>Why this exists at all.</b> It is <see cref="Push"/>'s inverse and nothing else: a column's ends
  /// are asymmetric on purpose - the raceway eats the bottom, the hopper lays on the top - so "take back
  /// what was just laid" is a genuinely different operation from descent and cannot be spelled with
  /// <see cref="Take"/>. Everything else about it is <see cref="Take"/>'s, deliberately and to the line: the
  /// same clamping, the same mid-band split, and <b>both halves keep the temperature</b> for the same
  /// reason - heat is a property of the material, not of the quantity.
  /// </para>
  /// <para>
  /// Never returns more than the column holds, and an empty column returns empty.
  /// </para>
  /// </summary>
  public List<ChargeSegment> TakeTop(int units)
  {
    List<ChargeSegment> taken = HighestUnits(units);

    int remaining = 0;
    foreach (ChargeSegment segment in taken)
      remaining += segment.Units;

    int spent = 0;
    while (spent < _segments.Count && remaining > 0)
    {
      int at = _segments.Count - 1 - spent;
      ChargeSegment segment = _segments[at];
      if (segment.Units > remaining)
      {
        // The boundary band survives, shorter and at the same temperature.
        _segments[at] = segment with { Units = segment.Units - remaining };
        remaining = 0;
      }
      else
      {
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
  /// raceway end and returns them, raceway-first - a <b>mid-span</b> removal, and what breaking a charge
  /// pile out of the shaft wall does to the column behind it.
  /// <para>
  /// <b>Why the column needs a third take at all.</b> Its two ends are asymmetric and both are already
  /// spoken for - <see cref="Take"/> is descent and <see cref="TakeTop"/> is the hopper's inverse - but a
  /// player digging into the wall reaches neither end. <b>And the escape hatch of "recoverable from the
  /// top" must not be taken:</b> a chill sits at the <em>bottom</em> of the shaft by definition, so a
  /// recovery that only works from the stockline leaves the one failure recoverability exists for
  /// unrecoverable, while the gate reads green. See <c>docs/design/layered-charge.md</c>.
  /// </para>
  /// <para>
  /// <b>Everything above the gap falls, and it costs one list rebuild.</b> That is the whole point of the
  /// column model: the behaviour that made vanilla's coal piles unusable - and drove this redesign - is free
  /// here, because there are no per-block writes to keep in step. <see cref="Append"/> lays the survivors
  /// back down, so the two sides of the gap <b>merge</b> when they meet as the same band, under exactly the
  /// rule <see cref="Push"/> uses. Without that a shaft dug into and refilled would accumulate a seam per
  /// repair.
  /// </para>
  /// <para>
  /// Clamped exactly as <see cref="Take"/> is, and for the same reason - a break is a live world event, not
  /// an authoring mistake. A span starting past the top, a non-positive count and an empty column all return
  /// empty; a span running off the top returns what was actually there.
  /// </para>
  /// <para>
  /// Boundary bands split, and <b>both halves keep the temperature</b> - the same rule as every other
  /// take. Heat is a property of the material, not of the quantity.
  /// </para>
  /// </summary>
  public List<ChargeSegment> TakeSpan(int fromUnit, int units)
  {
    var taken = new List<ChargeSegment>();
    if (units <= 0 || _segments.Count == 0)
      return taken;

    int from = Math.Max(0, fromUnit);
    int to = from + units; // exclusive

    var kept = new List<ChargeSegment>(_segments.Count + 2);
    int at = 0; // unit offset of the current band's raceway-end edge

    foreach (ChargeSegment segment in _segments)
    {
      int start = at;
      int end = at + segment.Units;
      at = end;

      if (end <= from || start >= to)
      {
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
  /// The highest <paramref name="units"/> the column holds, stockline end first, <b>without removing
  /// them</b> - so an interaction can see what it is about to be handed (which material, which mix) before
  /// committing. The mirror of <see cref="LowestUnits"/>, with the same clamping.
  /// </summary>
  public List<ChargeSegment> HighestUnits(int units)
  {
    var highest = new List<ChargeSegment>();
    int remaining = units;
    for (int i = _segments.Count - 1; i >= 0 && remaining > 0; i--)
    {
      ChargeSegment segment = _segments[i];
      int part = Math.Min(segment.Units, remaining);
      highest.Add(segment with { Units = part });
      remaining -= part;
    }
    return highest;
  }

  /// <summary>
  /// The lowest <paramref name="units"/> the column holds, raceway end first, <b>without removing them</b> -
  /// so the melt condition can be evaluated against exactly what a <see cref="Take"/> would yield before
  /// anything is committed. Same clamping as <see cref="Take"/>: never more than is there, empty for an
  /// empty column or a non-positive request.
  /// </summary>
  public List<ChargeSegment> LowestUnits(int units)
  {
    var lowest = new List<ChargeSegment>();
    int remaining = units;
    for (int i = 0; i < _segments.Count && remaining > 0; i++)
    {
      ChargeSegment segment = _segments[i];
      int part = Math.Min(segment.Units, remaining);
      lowest.Add(segment with { Units = part });
      remaining -= part;
    }
    return lowest;
  }

  /// <summary>
  /// Rewrites the column in place, one <b>block</b> at a time: every segment is first split where it
  /// crosses a block boundary, then each piece is handed to <paramref name="rewrite"/> along with the
  /// index of the block it falls in, and what comes back is laid down in its place.
  /// <para>
  /// <b>Why this exists at all, and why it is not spelled with <see cref="Take"/> + <see cref="Push"/>.</b>
  /// Burn-out scales each band's <i>fuel</i> part by how high up the shaft it sat while leaving iron and
  /// flux verbatim. Take-then-push cannot express that: the merge rule in <see cref="Push"/> would collapse
  /// the rewritten bands back into one stripe and the gradient - the whole point - would be lost. And
  /// <see cref="Segments"/> is genuinely read-only, so there is no cast back to a list either. This is the
  /// door, opened deliberately rather than found.
  /// </para>
  /// <para>
  /// <b>Per block, not per band or per segment.</b> A segment can be metres tall and a single retained
  /// fraction over the whole of it is exactly the thing that has no gradient in it; a band is finer than
  /// anything the world can show, because what the player digs out is a <em>block</em>. Splitting at block
  /// boundaries makes "what the pile at this cell holds" and "what this rewrite decided" the same question,
  /// which is what lets a salvage read at one cell be asserted exactly.
  /// </para>
  /// <para>
  /// A piece rewritten to <c>Units &lt;= 0</c> is <b>dropped</b> - that is how a consumer burns fuel away
  /// rather than leaving a zero-unit band behind. Pieces that come back identical re-merge under
  /// <see cref="Push"/>'s own rule, so a rewrite that changes nothing leaves the segment list as it found
  /// it instead of shattering it into one entry per block.
  /// </para>
  /// </summary>
  /// <param name="unitsPerBlock">The block quantum to split on - the furnace's
  /// <c>ChargeUnitsPerBlock</c>. Clamped to at least 1, like everywhere else it is read.</param>
  /// <param name="rewrite">Given a piece and the index of the block it sits in (0 at the column's floor),
  /// returns what should stand there instead.</param>
  public void Rewrite(
    int unitsPerBlock,
    Func<ChargeSegment, int, ChargeSegment> rewrite
  )
  {
    if (_segments.Count == 0)
      return;

    int perBlock = Math.Max(1, unitsPerBlock);
    var rewritten = new List<ChargeSegment>(_segments.Count);
    int offset = 0;

    foreach (ChargeSegment segment in _segments)
    {
      int remaining = segment.Units;
      while (remaining > 0)
      {
        int blockIndex = offset / perBlock;
        // How much of this segment is still inside the block the offset currently sits in.
        int room = ((blockIndex + 1) * perBlock) - offset;
        int part = Math.Min(remaining, room);

        ChargeSegment piece = rewrite(segment with { Units = part }, blockIndex);
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
  /// Rewrites the lowest <paramref name="units"/> in place - the raceway's own span - splitting whatever
  /// band the boundary falls in so the rest of it is left untouched.
  /// <para>
  /// <b>What this is for: burning.</b> The raceway consumes <em>carbon</em>, and carbon arrives two ways
  /// - as a fuel band, which burns away entirely and lets the column descend into the gap, and as the coke
  /// stamped into a burden band, which burns out of the stamp while the ore it was mixed with stays put
  /// until it melts. Only a rewrite can express the second: the band survives, shorter of carbon.
  /// </para>
  /// <para>
  /// Distinct from <see cref="Rewrite"/>, which walks the <b>whole</b> column a block at a time for
  /// burn-out's height gradient. This one is a span at the bottom and takes no block quantum: what the
  /// raceway reaches is a depth in units, not a count of blocks.
  /// </para>
  /// <para>
  /// A piece rewritten to <c>Units &lt;= 0</c> is dropped, which is how fuel burns away to nothing. Pieces
  /// that come back unchanged re-merge under <see cref="Push"/>'s rule, so a tick that burned nothing
  /// leaves the segment list exactly as it found it.
  /// </para>
  /// </summary>
  public void RewriteLowest(int units, Func<ChargeSegment, ChargeSegment> rewrite)
  {
    if (_segments.Count == 0 || units <= 0)
      return;

    var rewritten = new List<ChargeSegment>(_segments.Count);
    int left = units;

    foreach (ChargeSegment segment in _segments)
    {
      if (left <= 0)
      {
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

  /// <summary>The merge half of <see cref="Push"/>, factored out so <see cref="Rewrite"/> lays its pieces
  /// down under exactly the same rule rather than a second copy of it that could drift.</summary>
  private static void Append(List<ChargeSegment> into, ChargeSegment segment)
  {
    if (into.Count > 0)
    {
      ChargeSegment top = into[^1];
      if (
        top.Material == segment.Material
        && top.Mix == segment.Mix
        && Math.Abs(top.Temperature - segment.Temperature) <= TempMergeEpsilon
      )
      {
        int merged = top.Units + segment.Units;
        into[^1] = top with
        {
          Units = merged,
          Temperature =
            ((top.Temperature * top.Units) + (segment.Temperature * segment.Units))
            / merged,
        };
        return;
      }
    }

    into.Add(segment);
  }

  #endregion

  #region Counter-current

  // The furnace is counter-current: all the coke burns at the raceway (index 0) and nowhere else, and
  // the gas that leaves it rises through everything above, warming it. A band is therefore warmed by coke
  // that burned beneath it while it descended - never by spending its own - and what it carries when it
  // finally reaches the raceway is what decides whether it melts. That is the whole model, and it is why
  // "taller shaft is more efficient" and "hot blast saves coke" fall out with no multiplier anywhere.
  //
  // Both operations here are pure: no Api, no dt, no config. What varies per tick arrives as arguments.

  /// <summary>
  /// Passes hot gas up through the column from the raceway end, warming every band it meets, and returns
  /// the temperature of the gas <b>leaving the top</b> - the stack gas, which is what the furnace vents.
  /// <para>
  /// <b>Each unit takes its share, not each band.</b> A band of <c>U</c> units absorbs
  /// <c>1 - (1-transferFraction)^U</c> of whatever excess the gas still carries. Compounding per
  /// <em>unit</em> rather than per band is load-bearing: the segment list is cut wherever charging and
  /// <see cref="Take"/> happened to leave it, and this pass re-cuts it every tick, so a rule
  /// that counted bands would make the answer depend on the player's charging rhythm. Split any band in
  /// two and the gas leaves at exactly the same temperature having given up exactly the same heat; only
  /// the resolution of the internal gradient changes.
  /// </para>
  /// <para>
  /// <b>The equilibrium clamp is not defensive tidiness.</b> Two bodies exchanging heat cannot leave
  /// either one past their common temperature. Without it a thin band under a large gas flow ends hotter
  /// than the flame that warmed it, which then melts at the raceway on heat the furnace never made.
  /// </para>
  /// <para>
  /// <b>dt lives in <paramref name="gasHeatCapacityUnits"/>, not in a rate.</b> The gas crosses the whole
  /// shaft in seconds while the burden descends over hours, so within a tick the flow is quasi-steady: what
  /// scales with the step is how much gas passed, not how effective each metre of shaft was. That is what
  /// keeps the pass dt-proportional under <c>MaxAwayCatchupSteps</c>, which replays up to 600 one-second
  /// sub-ticks at chunk load - a rule that was not would melt a shaft on reload.
  /// </para>
  /// <para>
  /// A band the gas cannot warm is passed <b>untouched</b> rather than ending the walk. The profile is
  /// monotone in practice, but the flame falls whenever the blast or the coke fraction does, so a band
  /// carrying heat from a hotter stretch of the campaign sitting under a cooler gas is ordinary - and
  /// stopping there would leave the whole shaft above it cold for the rest of the campaign.
  /// </para>
  /// <para>
  /// An empty column, a non-positive or non-finite capacity, and a non-positive transfer all hand
  /// <paramref name="gasTemp"/> straight back and change nothing: an unlit or unburdened furnace ticks
  /// through here every second, so those are normal states rather than errors - and a NaN admitted once
  /// poisons every later compare, which is the failure this shape of guard already exists for in
  /// <see cref="FromTree"/>.
  /// </para>
  /// <para>
  /// <b>It only ever warms, and there is deliberately no ambient floor on it.</b> An earlier draft took
  /// one and it was dead weight - the gas that reaches a band always leaves it above ambient anyway, and
  /// on the paths where a floor would bite (no gas at all) this returns before touching a band. What the
  /// floor was reaching for is a <em>different</em> operation - a charged column losing its heat to the
  /// world once the fire dies - and that is <c>layered-charge.md</c> Open #1, still open and not built
  /// here. Do not smuggle it in as a clamp.
  /// </para>
  /// </summary>
  /// <param name="gasTemp">What leaves the raceway, in °C - the flame temperature, not the furnace's.</param>
  /// <param name="gasHeatCapacityUnits">How much charge this step's gas could warm by one degree for each
  /// degree it falls, in the same units the bands are counted in. Proportional to the coke burned in the
  /// step, so it carries dt.</param>
  /// <param name="transferFraction">The share of the gas's remaining excess that <b>one unit</b> of charge
  /// absorbs. Sets how fast the profile develops, and therefore how visible the thermal-reserve zone is.</param>
  /// <param name="resolutionUnits">How finely the profile is resolved - the tallest piece of charge that
  /// is allowed to be one temperature. <b>Load-bearing, not a performance knob.</b> A column charged in
  /// one go is <em>one band</em>, and a band warms uniformly, so without this a freshly charged shaft has
  /// no temperature profile at all: the whole 640 units climb together and nothing melts until all of it
  /// is at the melting point. Splitting as the gas passes is what gives the profile somewhere to live -
  /// hot at the raceway, cooling upward. Every piece is laid back down through <see cref="Push"/>'s own
  /// merge rule, so whatever the gas left identical folds straight back together and the list does not grow. One <b>block</b> is the right size: it is what the
  /// player digs out, what burn-out interpolates over, and what a pile's glow already reads.</param>
  public float RiseGasThrough(
    float gasTemp,
    float gasHeatCapacityUnits,
    float transferFraction,
    int resolutionUnits
  )
  {
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
    foreach (ChargeSegment segment in _segments)
    {
      int left = segment.Units;
      float start = segment.Temperature;
      while (left > 0)
      {
        int piece = Math.Min(left, resolution);
        left -= piece;

        float rise = 0f;
        if (gas > start)
        {
          float absorbed = 1f - MathF.Pow(retained, piece);
          float equilibrium =
            ((capacity * gas) + (piece * start)) / (capacity + piece);
          rise = Math.Max(
            0f,
            Math.Min(capacity * (gas - start) * absorbed / piece, equilibrium - start)
          );
        }

        // Laid down through Push's own merge rule, so pieces the gas left identical fold straight back
        // together and a pass that changed nothing leaves the list exactly as it found it.
        Append(warmed, segment with { Units = piece, Temperature = start + rise });
        gas -= rise * piece / capacity;
      }
    }

    _segments.Clear();
    _segments.AddRange(warmed);
    return gas;
  }

  /// <summary>
  /// The hottest band overlapping units <c>[fromUnit, fromUnit + units)</c>, in °C, or <c>0</c> where the
  /// span holds nothing. Clamped like every other span read here: a span off either end simply reports
  /// whatever part of it is real.
  /// <para>
  /// <b>Hottest, not average, and that is the drawing's constraint speaking.</b> A block carries one
  /// light value and draws up to sixteen bands, so it has to pick one - and a block with a white-hot band
  /// in it should look lit even when the rest of what it draws is cold. Averaging would put the whole
  /// shaft at a dull middle and lose the one thing the profile is worth seeing for.
  /// </para>
  /// </summary>
  public float PeakTemperature(int fromUnit, int units)
  {
    if (units <= 0)
      return 0f;

    int from = Math.Max(0, fromUnit);
    long to = (long)from + units;
    float peak = 0f;
    bool any = false;
    int at = 0;

    foreach (ChargeSegment segment in _segments)
    {
      int end = at + segment.Units;
      if (end > from && at < to)
      {
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

  // What the blocks over the column draw. Pure functions of the segment list plus a band size, so the
  // renderer and the block placer can both be tested with no scene and at any band size - which is why
  // the band size arrives as a parameter rather than being read off IwexValues in here.

  /// <summary>
  /// How many charge blocks tall a column of <paramref name="units"/> units stands, at
  /// <paramref name="unitsPerBlock"/> units per block. <c>SyncChargeBlocks</c> places and removes
  /// <c>iwex:furnace-chargepile</c> as this crosses a boundary.
  /// <para>
  /// A part-filled band still needs a block to draw it, so this rounds up twice - units to bands, bands
  /// to blocks. At the shipped 8 u a block holds 128 u: 128 u is one block and 129 u is two.
  /// </para>
  /// </summary>
  public static int BlocksTall(int units, int unitsPerBlock)
  {
    if (units <= 0)
      return 0;
    long perBlock = Math.Max(1, unitsPerBlock);
    return (int)(((units - 1) / perBlock) + 1);
  }

  /// <summary>
  /// What the charge block at height index <paramref name="blockIndex"/> draws: bands
  /// <c>blockIndex·16 … (blockIndex+1)·16</c> of the column, raceway end first, run-length encoded.
  /// <para>
  /// <b>Bands do not snap to block boundaries.</b> The window is cut out of the column's own continuous
  /// band sequence, so a course taller than a block runs off the top of one block and straight on across
  /// the bottom of the next as one stripe - which is what a furnace charge looks like in section, and the
  /// visual the whole model is aiming at.
  /// </para>
  /// <para>
  /// A band that straddles two segments draws the one filling <b>most</b> of it, ties going to the lower
  /// (raceway-nearer) segment. Reading the band's lower edge or its midpoint instead would let a boundary
  /// a unit off centre flip a whole band to a material that barely appears in it; largest-share also means
  /// a segment thinner than half a band is simply below the wall's resolution rather than drawn as if it
  /// were a full course.
  /// </para>
  /// <para>
  /// Returns fewer than <see cref="BandsPerBlock"/> bands' worth - possibly none - where the column ends
  /// inside the block, so the top block draws only the charge that is actually there. <b>Empty for any
  /// <paramref name="blockIndex"/> outside the column</b>, negative or above the top block, at any
  /// magnitude: the renderer subtracts two world Y values to get this, and a stale one is garbage rather
  /// than a small error.
  /// </para>
  /// </summary>
  public List<ChargeBandRun> BandsAt(int blockIndex, int unitsPerBlock)
  {
    var runs = new List<ChargeBandRun>();
    if (blockIndex < 0)
      return runs;

    long perBlock = Math.Max(1, unitsPerBlock);
    int total = TotalUnits;

    // Walks with the bands rather than restarting per band: bands ascend, so a segment already left
    // behind can never be wanted again.
    int seg = 0;
    int segBase = 0;

    for (int i = 0; i < BandsPerBlock; i++)
    {
      // Widened, because this multiply is what turns "above the stockline" into "a phantom full
      // block". In int it wraps at blockIndex 16 777 216 (at the shipped 8 u a band) and lands
      // negative - below the guard above, which has already run - so `low >= total` was false, the
      // overlap arithmetic came out negative, never beat best = 0, and the whole block drew the
      // pre-seeded material of the bottom segment. The lower guard therefore needs no upper twin: a
      // band that starts past the charge simply ends the loop, at any index a caller can pass.
      // Band boundaries are derived from the block quantum rather than a per-band one, which is what
      // lets a remelt pile work at all: its 3 000 u/block over 16 bands is 187.5 u a band, and a
      // per-band integer cannot express that. Multiplying first and dividing after keeps it exact.
      long band = ((long)blockIndex * BandsPerBlock) + i;
      long lowUnits = (band * perBlock) / BandsPerBlock;
      if (lowUnits >= total)
        break;
      long highUnits = ((band + 1) * perBlock) / BandsPerBlock;
      // Both narrowings are safe: 0 <= lowUnits < total <= int.MaxValue, and high is clamped to total.
      int low = (int)lowUnits;
      int high = (int)Math.Min(highUnits, total);

      while (seg < _segments.Count && segBase + _segments[seg].Units <= low)
      {
        segBase += _segments[seg].Units;
        seg++;
      }
      if (seg >= _segments.Count)
        break;

      string material = _segments[seg].Material;
      BurdenMix mix = _segments[seg].Mix;
      int best = 0;
      for (int s = seg, at = segBase; s < _segments.Count && at < high; s++)
      {
        ChargeSegment segment = _segments[s];
        int end = at + segment.Units;
        int overlap = Math.Min(end, high) - Math.Max(at, low);
        // Strictly greater, so an exact tie keeps the lower segment and the rule stays deterministic.
        if (overlap > best)
        {
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
  /// Writes the column into <paramref name="tree"/> as four parallel arrays. Always writes, including for an
  /// empty column - otherwise a column that has just been drawn down to nothing would reload with whatever
  /// the previous save left behind.
  /// </summary>
  public void ToTree(ITreeAttribute tree)
  {
    int count = _segments.Count;
    var materials = new string[count];
    var units = new int[count];
    var temperatures = new float[count];
    var mix = new float[count * 3];

    for (int i = 0; i < count; i++)
    {
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
  /// than throwing on load, and drops any band the save cannot describe - see the loop.
  /// </summary>
  public void FromTree(ITreeAttribute tree)
  {
    _segments.Clear();

    int[] units = (tree[UnitsKey] as IntArrayAttribute)?.value ?? [];
    string[] materials =
      (tree[MaterialsKey] as StringArrayAttribute)?.value ?? [];
    float[] temperatures = (tree[TempsKey] as FloatArrayAttribute)?.value ?? [];
    float[] mix = (tree[MixKey] as FloatArrayAttribute)?.value ?? [];

    int count = Math.Min(units.Length, materials.Length);
    for (int i = 0; i < count; i++)
    {
      // A hand-edited, truncated or version-skewed save can carry a band no push could have made, and a
      // dropped band is by far the mildest way to fail on it. A negative count is the dangerous one: it
      // runs Take's clamp backwards, so the raceway is handed 3 u while 8 u leave the column, and merging
      // onto it divides by a zero unit total for a NaN temperature that then poisons every later compare.
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
