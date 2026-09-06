using System;
using System.Linq;
using ExpandedLib.Catalogues;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The spec a roll set carries, parsed from the roll-set item's <c>rollset</c> attribute; the tooling owns
/// the data and the machine only reads it, as the casting patterns do (<see cref="Casting.MoldSpec"/>).
/// <para>
/// It declares what the tooling itself decides - which roller family it is, which stock it will bite, how
/// wide its barrel is and what torque it needs to turn - and nothing about what the metal becomes. The
/// states the metal passes through are the stock family's stage route, and the pair of them is a
/// <see cref="MillSchedule"/>, so a set names no product and a new product needs no set edited. See
/// docs/design/machines/rolling-mill.md and docs/design/mechanics/process-extension.md.
/// </para>
/// </summary>
/// <param name="Schema">Schema version of the declaration, so a parser can read every shipped form.</param>
/// <param name="Family">The roller family - <c>flat</c> or <c>grooved</c> in what we ship, and whatever a mod names in what it ships. Selects this set's branch of a stage route.</param>
/// <param name="Accepts">Stock forms this set will bite (e.g. <c>bloom</c>, <c>billet</c>, <c>slab</c>). Empty accepts nothing.</param>
/// <param name="BarrelWidth">Usable width of the roll barrel. Stock wider than this cannot be taken in one bite and needs side-by-side passes.</param>
/// <param name="MinTorque">Drive torque the stand needs before this set will turn at all. Tiers gate on torque, not roll material.</param>
public sealed record RollSetSpec(
  int Schema,
  string Family,
  string[] Accepts,
  float BarrelWidth,
  float MinTorque
) {
  /// <summary>The attribute key a roll set carries its spec under.</summary>
  public const string AttributeKey = "rollset";

  /// <summary>The schema this parser writes and reads up to. Raise it only alongside the fallback that
  /// reads the form it replaces (<see cref="SpecSchema"/>).</summary>
  public const int CurrentSchema = SpecSchema.First;

  /// <summary>Working passes a single gap costs for stock of <paramref name="width"/>: a floor of two (a
  /// pass, then the piece turned over and passed again), multiplied by the strip count when the stock
  /// overhangs the barrel and has to be rolled side by side.</summary>
  public int PassesAt(float width) {
    if (BarrelWidth <= 0f || width <= BarrelWidth)
      return 2;
    return 2 * (int)MathF.Ceiling(width / BarrelWidth);
  }

  /// <summary>Whether stock of <paramref name="width"/> overhangs the barrel and so has to be taken in
  /// side-by-side strips rather than one bite.</summary>
  public bool OverhangsBarrel(float width) =>
    BarrelWidth > 0f && width > BarrelWidth;

  /// <summary>Whether this set will bite <paramref name="form"/> at all. Independent of the stage route:
  /// the route says which states the metal has, this says whether the tooling can take that stock at
  /// all - a narrow barrel refuses a slab whatever states the slab has.</summary>
  public bool AcceptsForm(string? form) =>
    form != null && Accepts.Contains(form);

  /// <summary>Parses and validates a roll set's <c>rollset</c> attribute. Returns false with a
  /// human-readable <paramref name="error"/> on any malformed field, so a bad set fails at load rather than
  /// at the mill.</summary>
  public static bool TryParse(
    JsonObject? node,
    out RollSetSpec? spec,
    out string? error
  ) {
    spec = null;
    error = null;

    if (node is not { Exists: true }) {
      error = $"missing '{AttributeKey}' attribute";
      return false;
    }

    if (!SpecSchema.TryRead(node, CurrentSchema, out int schema, out error))
      return false;

    string family = node["family"].AsString("");
    if (string.IsNullOrWhiteSpace(family)) {
      error =
        "missing 'family' (nothing selects the set's branch of a stage route without it)";
      return false;
    }

    string[] accepts =
    [
      .. (node["accepts"].AsArray<string>([]) ?? []).Where(a =>
        !string.IsNullOrWhiteSpace(a)
      )!,
    ];
    if (accepts.Length == 0) {
      error = "missing 'accepts' forms (a set that bites nothing is useless)";
      return false;
    }

    float barrelWidth = node["barrelWidth"].AsFloat(0f);
    if (barrelWidth <= 0f) {
      error =
        "missing 'barrelWidth' (the mill cannot tell whether stock overhangs the rolls without it)";
      return false;
    }

    spec = new RollSetSpec(
      schema,
      family,
      accepts,
      barrelWidth,
      node["minTorque"].AsFloat(0f)
    );
    return true;
  }
}
