using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Structures;

/// <summary>
/// Resolves a block's <c>attributes.multiblockLayout</c> - an ASCII grid, the JSON twin of
/// <see cref="MultiblockLayoutBuilder"/> - into the same <c>multiblockStructure</c>, and, absent an
/// explicit <c>fillerOffsets</c>, the matching footprint, a code-first definition would have emitted.
/// The grid is <c>{ origin: [xLeft, zTop], legend: { symbol: code }, layers: [ [row, ...], ... ], core:
/// symbol }</c>, one <c>layers</c> entry per Y level; <c>origin</c> and <c>core</c> are optional. Run
/// once per <see cref="Block"/> instance from <see cref="BlockFilledMegastructure.OnLoaded"/>, before any
/// placement. A malformed attribute logs one Error and leaves the block without a
/// <c>multiblockStructure</c>, so the structure never completes rather than throwing at chunk load.
/// </summary>
public static class JsonMultiblockLayout {
  // Blocks are singletons per registered variant, one instance per side; resolving twice would either
  // redo the work for nothing or, on a malformed layout, log the same Error once per block entity ever
  // placed. Reference equality: two variants of one block code are two different Block instances.
  private static readonly HashSet<Block> _resolved = new(
    ReferenceEqualityComparer.Instance
  );

  /// <summary>
  /// Resolves <paramref name="block"/>'s <c>multiblockLayout</c> attribute, if any, into
  /// <c>multiblockStructure</c> and, when the block declares no <c>fillerOffsets</c> of its own, into a
  /// derived footprint. Idempotent per block instance; safe to call from every subclass's
  /// <c>OnLoaded</c>, including one that never declares the attribute.
  /// </summary>
  public static void Resolve(Block block, ILogger logger) {
    lock (_resolved) {
      if (!_resolved.Add(block))
        return;
    }

    JsonObject? layoutAttr = block.Attributes?["multiblockLayout"];
    if (layoutAttr is not { Exists: true })
      return;

    try {
      var layout = (JObject)layoutAttr.Token!;
      var attrs = (JObject)block.Attributes!.Token!;

      JObject structure = Build(layout);
      attrs["multiblockStructure"] = structure;

      if (attrs["fillerOffsets"] == null)
        attrs["fillerOffsets"] = ExBlockDef.SerializeFillerCells(
          DerivedFillerCells(structure)
        );
    } catch (Exception ex) {
      logger.Error(
        "[exlib] {0} declares a malformed multiblockLayout ({1}); the structure will never complete.",
        block.Code,
        ex.Message
      );
    }
  }

  // Replays the grid through the same builder a code-first definition drives, so the emitted
  // multiblockStructure is byte-for-byte what MultiblockLayoutBuilder.Build() would have produced.
  private static JObject Build(JObject layout) {
    var builder = new MultiblockLayoutBuilder();

    if (layout["origin"] is JArray origin && origin.Count >= 2)
      builder.Origin((int)origin[0]!, (int)origin[1]!);

    if (layout["legend"] is not JObject legend)
      throw new InvalidOperationException(
        "multiblockLayout has no 'legend' object."
      );
    foreach (JProperty entry in legend.Properties()) {
      if (entry.Name.Length != 1)
        throw new InvalidOperationException(
          $"multiblockLayout legend key '{entry.Name}' is not a single character."
        );
      builder.Legend(entry.Name[0], (string)entry.Value!);
    }

    if (layout["layers"] is not JArray layers)
      throw new InvalidOperationException(
        "multiblockLayout has no 'layers' array."
      );
    for (int y = 0; y < layers.Count; y++) {
      if (layers[y] is not JArray rows)
        throw new InvalidOperationException(
          $"multiblockLayout layer {y} is not an array of rows."
        );
      var grid = new string[rows.Count];
      for (int r = 0; r < rows.Count; r++)
        grid[r] = (string)rows[r]!;
      builder.Layer(y, string.Join("\n", grid));
    }

    if (layout["core"] is JValue coreVal) {
      string core = (string)coreVal!;
      if (core.Length != 1)
        throw new InvalidOperationException(
          $"multiblockLayout 'core' must be a single character, got '{core}'."
        );
      builder.Core(core[0]);
    }

    return builder.Build();
  }

  // Every drawn cell but the principal's own (0,0,0) - the layout's non-empty, non-core cells - as a
  // plain (no attach, no behaviours) footprint, the same shape ExBlockDef.FillerOffsets emits.
  private static IReadOnlyList<FillerCellSpec> DerivedFillerCells(
    JObject structure
  ) {
    var cells = new List<FillerCellSpec>();
    if (structure["offsets"] is JArray offsets)
      foreach (JToken offset in offsets) {
        int x = (int)offset["x"]!;
        int y = (int)offset["y"]!;
        int z = (int)offset["z"]!;
        if (x == 0 && y == 0 && z == 0)
          continue; // the principal occupies the origin, never a filler
        cells.Add(new FillerCellSpec(x, y, z));
      }
    return cells;
  }
}
