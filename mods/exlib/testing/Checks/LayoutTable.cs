using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// Reads a code-first multiblock layout's emitted table back into a per-cell block code, for a test to
/// pin against the drawing rather than the JSON. Content-free: every entry point reads whatever
/// <see cref="ExBlockDef.MultiblockLayout"/> emitted, so a mod's own layout oracle stays a thin wrapper
/// around this rather than a second parser.
/// </summary>
public static class LayoutTable {
  /// <summary>
  /// The wanted block code at each structure-local cell, read out of the generated
  /// <c>multiblockStructure</c> attribute with block numbers resolved through <c>blockNumbers</c> - the
  /// table the game builds the structure from, not a restatement of the ASCII drawing.
  /// </summary>
  public static Dictionary<Vec3i, string> From(ExBlockDef def) {
    JObject structure = (JObject)
      def.ToJson()["attributes"]!["multiblockStructure"]!;

    var codeByNumber = ((JObject)structure["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    var cells = new Dictionary<Vec3i, string>();
    foreach (JToken offset in (JArray)structure["offsets"]!)
      cells[
        new Vec3i((int)offset["x"]!, (int)offset["y"]!, (int)offset["z"]!)
      ] = codeByNumber[(int)offset["w"]!];
    return cells;
  }

  /// <summary>
  /// The wanted block code at each world-relative cell of the structure the game builds from the
  /// anchor's definition, rotated by <paramref name="angle"/> through vanilla
  /// <see cref="MultiblockStructure"/> - loaded and rotated the way the production block entity does at
  /// placement; the definition carries no world-placed attributes, so this reads the definition, not an
  /// instance.
  /// </summary>
  public static Dictionary<Vec3i, string> Rotated(ExBlockDef def, int angle) {
    JObject json = (JObject)def.ToJson()["attributes"]!["multiblockStructure"]!;

    // Authored glyph per block number, read straight off the JSON as From does, so a domainless or
    // wildcard code keeps its authored form rather than being re-domained by an AssetLocation round-trip.
    var codeByNumber = ((JObject)json["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    // The rotation comes from vanilla MultiblockStructure, rotated the way the production block entity
    // does at placement.
    MultiblockStructure structure = new JsonObject(
      json
    ).AsObject<MultiblockStructure>()!;
    structure.InitForUse(angle);

    var cells = new Dictionary<Vec3i, string>();
    foreach (BlockOffsetAndNumber o in structure.TransformedOffsets)
      cells[new Vec3i(o.X, o.Y, o.Z)] = codeByNumber[o.W];
    return cells;
  }
}
