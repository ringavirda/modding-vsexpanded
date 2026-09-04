using System.Collections.Generic;
using ExpandedLib.Processes;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Casting;

/// <summary>Which casting station a mold pattern is rammed into.</summary>
public enum MoldSize {
  /// <summary>The 1×1 casting cell.</summary>
  Cell,

  /// <summary>The 1×2 long cell.</summary>
  LongCell,
}

/// <summary>
/// The spec a mold pattern carries, parsed from the pattern item's <c>mold</c> attribute: which
/// rammed-sand mesh to show, how much metal the impression holds, where the molten surface renders, what
/// shake-out yields, and the minimum pour temperature. The pattern owns this data and the casting cell
/// only reads it, so a mod adds a castable part with a pattern definition alone.
/// </summary>
/// <param name="Schema">Schema version of the declaration, so a parser can read every shipped form.</param>
/// <param name="Size">Which station this pattern is for.</param>
/// <param name="Shape">The filling-shape asset shown while the impression is present (e.g. <c>iiex:casting/cell-filling-plate</c>).</param>
/// <param name="Capacity">Units of metal the impression holds (= the cavity volume).</param>
/// <param name="Cavity">Molten-renderer fill boxes in 16-space (the surface the poured metal shows on).</param>
/// <param name="Output">What a full, hardened cast yields on shake-out (resolved against the world at use time).</param>
/// <param name="MinPourTemp">Pour temperature (°C) below which a completed fill is a misrun (scrap, not the part). 0 disables the check.</param>
public sealed record MoldSpec(
  int Schema,
  MoldSize Size,
  string Shape,
  int Capacity,
  Cuboidf[] Cavity,
  JsonItemStack Output,
  float MinPourTemp
) {
  /// <summary>The attribute key a pattern carries its spec under.</summary>
  public const string AttributeKey = "mold";

  /// <summary>The schema this parser writes and reads up to. Raise it only alongside the fallback that
  /// reads the form it replaces (<see cref="SpecSchema"/>).</summary>
  public const int CurrentSchema = SpecSchema.First;

  /// <summary>
  /// Parses and validates a pattern's <c>mold</c> attribute. Returns false with a human-readable
  /// <paramref name="error"/> naming the malformed field.
  /// </summary>
  public static bool TryParse(
    JsonObject? mold,
    out MoldSpec? spec,
    out string? error
  ) {
    spec = null;
    error = null;

    if (mold is not { Exists: true }) {
      error = $"missing '{AttributeKey}' attribute";
      return false;
    }

    if (!SpecSchema.TryRead(mold, CurrentSchema, out int schema, out error))
      return false;

    string sizeStr = mold["size"].AsString("cell");
    MoldSize? size = sizeStr switch {
      "cell" => MoldSize.Cell,
      "longcell" => MoldSize.LongCell,
      _ => null,
    };
    if (size is null) {
      error = $"invalid size '{sizeStr}' (expected 'cell' or 'longcell')";
      return false;
    }

    string shape = mold["shape"].AsString("");
    if (string.IsNullOrWhiteSpace(shape)) {
      error = "missing 'shape'";
      return false;
    }

    int capacity = mold["capacity"].AsInt(0);
    if (capacity <= 0) {
      error = $"capacity must be > 0 (was {capacity})";
      return false;
    }

    JsonObject[]? cavityNodes = mold["cavity"].AsArray();
    if (cavityNodes is not { Length: > 0 }) {
      error = "missing 'cavity' boxes";
      return false;
    }
    var boxes = new List<Cuboidf>(cavityNodes.Length);
    foreach (JsonObject node in cavityNodes) {
      Cuboidf? box = node.AsObject<Cuboidf>(null);
      if (
        box is null
        || box.X2 <= box.X1
        || box.Y2 <= box.Y1
        || box.Z2 <= box.Z1
      ) {
        error = "a 'cavity' box is malformed (need x1<x2, y1<y2, z1<z2)";
        return false;
      }
      boxes.Add(box);
    }

    JsonItemStack? output = mold["output"].AsObject<JsonItemStack>(null);
    if (output?.Code is null) {
      error = "missing 'output' stack";
      return false;
    }

    float minPourTemp = mold["minPourTemp"].AsFloat(0f);

    spec = new MoldSpec(
      schema,
      size.Value,
      shape,
      capacity,
      boxes.ToArray(),
      output,
      minPourTemp
    );
    return true;
  }
}
