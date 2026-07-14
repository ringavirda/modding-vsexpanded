using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// The shared oracle for code-first definition parity tests: does an authored <c>ExBlockDef</c> reproduce the
/// hand-written blocktype JSON it replaced? Comparison is <b>semantic</b>, matching what the game actually
/// distinguishes, so a migration is judged equivalent iff the game would load an identical block:
/// <list type="bullet">
/// <item>numbers are type-agnostic (a JSON <c>0</c> equals the builder's <c>0.0</c>);</item>
/// <item><c>attributes.multiblockStructure</c> is compared as the SET of <c>(x,y,z,block-code)</c> cells it
/// resolves to - the offset order and the private <c>w</c> block-number values carry no meaning to the engine,
/// so a layer-DSL that renumbers/reorders the same cells is equivalent;</item>
/// <item><c>attributes.fillerOffsets</c> is compared as the SET of <c>(x,y,z,allowAttach,behaviors)</c> cells
/// (order is irrelevant - each filler is placed independently - but a cell's hosted <c>behaviors</c>, e.g. an
/// MP power port, ARE part of its identity and must match).</item>
/// </list>
/// Everything else (variantgroups, behaviors, construction stages, …) stays order-sensitive, because those
/// arrays ARE ordered in the schema. Replaces the per-mod copies of this normalizer.
/// </summary>
public static class DefinitionParity
{
  /// <summary>True when <paramref name="actual"/> is semantically the same blocktype as
  /// <paramref name="expected"/>. On failure, <paramref name="normalizedActual"/> holds the canonical form of
  /// the actual token for a readable assertion message.</summary>
  public static bool Equal(JToken expected, JToken actual, out string normalizedActual)
  {
    JToken na = Normalize(null, actual);
    normalizedActual = na.ToString();
    return JToken.DeepEquals(Normalize(null, expected), na);
  }

  /// <summary>Convenience overload without the diagnostic out-parameter.</summary>
  public static bool Equal(JToken expected, JToken actual) => Equal(expected, actual, out _);

  private static JToken Normalize(string? key, JToken token)
  {
    switch (token)
    {
      case JObject obj when key == "multiblockStructure":
        return CanonicalMultiblock(obj);
      case JArray arr when key == "fillerOffsets":
        return CanonicalCells(arr);
      case JObject obj:
        var normObj = new JObject();
        foreach (JProperty p in obj.Properties())
          normObj[p.Name] = Normalize(p.Name, p.Value);
        return normObj;
      case JArray arr:
        var normArr = new JArray();
        foreach (JToken item in arr)
          normArr.Add(Normalize(null, item));
        return normArr;
      case JValue { Type: JTokenType.Integer or JTokenType.Float } v:
        return new JValue(v.Value<double>());
      default:
        return token.DeepClone();
    }
  }

  // A multiblockStructure reduces to the SET of resolved cells plus the SET of referenced block codes; the
  // w-numbering and offset order are engine-internal and carry no meaning.
  private static JToken CanonicalMultiblock(JObject structure)
  {
    var codeOf = new Dictionary<int, string>();
    if (structure["blockNumbers"] is JObject numbers)
      foreach (JProperty p in numbers.Properties())
        codeOf[(int)p.Value!] = p.Name;

    var cells = new List<string>();
    if (structure["offsets"] is JArray offsets)
      foreach (JToken off in offsets)
      {
        int w = (int)off["w"]!;
        string code = codeOf.TryGetValue(w, out string? c) ? c : $"?w{w}";
        cells.Add($"{(int)off["x"]!},{(int)off["y"]!},{(int)off["z"]!}={code}");
      }
    cells.Sort();

    return new JObject
    {
      ["blockCodes"] = new JArray(codeOf.Values.OrderBy(c => c).Cast<object>().ToArray()),
      ["cells"] = new JArray(cells.Cast<object>().ToArray()),
    };
  }

  // A fillerOffsets array reduces to its cells sorted, with allowAttach made explicit (absent == false) and each
  // cell's hosted behaviors normalized (absent == empty) - so a port declared on a filler cell is verified, not
  // dropped. Behaviors keep their array order (small, authored lists) but their object keys compare unordered.
  private static JToken CanonicalCells(JArray cells)
  {
    var canon = cells
      .Select(c =>
        (
          X: (int)c["x"]!,
          Y: (int)c["y"]!,
          Z: (int)c["z"]!,
          Attach: (bool?)c["allowAttach"] ?? false,
          Behaviors: c["behaviors"] is JArray b
            ? Normalize(null, b)
            : new JArray()
        )
      )
      .OrderBy(c => c.X)
      .ThenBy(c => c.Y)
      .ThenBy(c => c.Z);

    var arr = new JArray();
    foreach (var c in canon)
      arr.Add(
        new JObject
        {
          ["x"] = c.X,
          ["y"] = c.Y,
          ["z"] = c.Z,
          ["allowAttach"] = c.Attach,
          ["behaviors"] = c.Behaviors,
        }
      );
    return arr;
  }
}
