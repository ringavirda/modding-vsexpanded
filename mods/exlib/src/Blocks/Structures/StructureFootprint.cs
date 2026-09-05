using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// One behaviour hosted by a footprint filler cell: a block-entity behaviour code plus, optionally, the block
/// face it exposes a connector on and a <see cref="Properties"/> config blob. Makes a filler cell a live port
/// (an <c>exlib.BEBehaviorMPFillerPort</c> on <c>west</c> turns the cell into a mechanical-power intake driven
/// by an axle on that face) or a stateful cell (an <c>exlib.BEBehaviorMoltenCell</c> whose
/// <c>{ capacity, flowSource, drainFitting }</c> vary per footprint cell, as on a casting bed's basin, runners
/// and molds). Serialized as <c>{ code[, face][, properties] }</c> in a cell's <c>behaviors</c> array;
/// <see cref="Properties"/> is any object.
/// </summary>
public readonly record struct FillerBehaviorSpec(
  string Code,
  string? Face = null,
  object? Properties = null
) {
  /// <summary>
  /// The same spec named by type: <c>Of&lt;BEBehaviorMPFillerPort&gt;("west")</c> resolves
  /// <typeparamref name="T"/>'s registered key, so renaming the behaviour class is a compile error.
  /// The string overload remains for a vanilla behaviour or one this assembly cannot reference; a
  /// hand-typed code that resolves to nothing is a per-cell warning at chunk load and the mega-block
  /// still assembles, so the machine places and does nothing.
  /// </summary>
  public static FillerBehaviorSpec Of<T>(
    string? face = null,
    object? properties = null
  )
    where T : Vintagestory.API.Common.BlockEntityBehavior =>
    new(
      // The type's own assembly supplies the domain, so the fallback is unused for any type whose
      // assembly declares [assembly: ExDomain].
      ExpandedLib.Registries.Entities.EntityRegistry.KeyFor(
        string.Empty,
        typeof(T)
      ),
      face,
      properties
    );
}

/// <summary>
/// One north-orientation footprint cell for a mega-block, authored in C#: the offset from the principal, whether
/// other blocks may attach to the filler placed there, and any per-cell hosted <see cref="FillerBehaviorSpec"/>
/// behaviours (MP/pipe ports). A cell may also carry a passive <see cref="PortFace"/> / <see cref="PortNetworkType"/>
/// network port: the face code the cell answers back through <see cref="BlockStructureFiller.HasConnectorAt"/> for
/// the principal, without hosting a <c>BEBehaviorNetworkMember</c> and so without joining the network as a graph
/// node. This is the typed source the code-first <c>fillerOffsets</c> attribute is
/// serialized from (<see cref="ExpandedLib.Definitions.ExBlockDef.FillerOffsets"/>), so a footprint is computed
/// and validated in C# rather than hand-typed as a coordinate array.
/// </summary>
public readonly record struct FillerCellSpec(
  int X,
  int Y,
  int Z,
  bool AllowAttach = false,
  IReadOnlyList<FillerBehaviorSpec>? Behaviors = null,
  IReadOnlyList<Cuboidf>? CollisionBoxes = null,
  string? PortFace = null,
  string? PortNetworkType = null
);

/// <summary>
/// The half-cell volumes a partially-filled footprint cell can take, named by the face the solid half sits
/// against - <c>Down</c> is a floor slab, <c>North</c> a slab against the north face. Machine layouts are
/// authored in these terms, so the drawing and the code use one vocabulary.
/// </summary>
public static class FillerSlab {
  /// <summary>The half of the cell against <paramref name="face"/>, as a single north-orientation cuboid.
  /// <see cref="StructureFillers.FootprintCells"/> rotates it into the placed orientation.</summary>
  public static Cuboidf Half(BlockFacing face) =>
    face.Index switch {
      BlockFacing.indexNORTH => new Cuboidf(0f, 0f, 0f, 1f, 1f, 0.5f),
      BlockFacing.indexSOUTH => new Cuboidf(0f, 0f, 0.5f, 1f, 1f, 1f),
      BlockFacing.indexEAST => new Cuboidf(0.5f, 0f, 0f, 1f, 1f, 1f),
      BlockFacing.indexWEST => new Cuboidf(0f, 0f, 0f, 0.5f, 1f, 1f),
      BlockFacing.indexUP => new Cuboidf(0f, 0.5f, 0f, 1f, 1f, 1f),
      _ => new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f),
    };
}

/// <summary>
/// Computes mega-block footprints (the <c>fillerOffsets</c> tables) from a compact description instead of
/// listing every cell by hand. Generated cells are validated by <see cref="Validate"/> at build time, so a
/// duplicate or origin-overlapping cell fails at load rather than clobbering a filler.
/// </summary>
public static class StructureFootprint {
  /// <summary>
  /// A rectangular floor footprint: <paramref name="depth"/> rows along +Z (<c>z = 0 .. depth-1</c>) and
  /// <c>2*halfWidth + 1</c> columns along X, emitted centre-out per row (<c>0, +1, -1, +2, -2, …</c>). The
  /// principal cell (the origin <c>0,0,0</c>) is skipped, and every flanking column (<c>x != 0</c>) opts
  /// into attachment.
  /// </summary>
  public static IReadOnlyList<FillerCellSpec> Rectangle(
    int halfWidth,
    int depth
  ) {
    if (halfWidth < 0)
      throw new ArgumentOutOfRangeException(nameof(halfWidth));
    if (depth < 1)
      throw new ArgumentOutOfRangeException(nameof(depth));

    var cells = new List<FillerCellSpec>();
    for (int z = 0; z < depth; z++)
      foreach (int x in ColumnsCentreOut(halfWidth)) {
        if (x == 0 && z == 0)
          continue; // the principal sits at the origin
        cells.Add(new FillerCellSpec(x, 0, z, AllowAttach: x != 0));
      }

    Validate(cells);
    return cells;
  }

  /// <summary>
  /// Builds a footprint from ASCII layer diagrams, the same drawing model as the multiblock DSL: one
  /// <c>Layer</c> per Y level, cells marked solid or attach-allowing. By default <c>'#'</c> is a plain filler
  /// and <c>'+'</c> a filler other blocks may attach to; register more with <see cref="FillerLayoutBuilder.Solid"/>
  /// / <see cref="FillerLayoutBuilder.Attach"/>. The principal origin <c>(0,0,0)</c> is skipped if drawn, and the
  /// result is validated for duplicate cells.
  /// </summary>
  public static IReadOnlyList<FillerCellSpec> Layout(
    Action<FillerLayoutBuilder> configure
  ) {
    var builder = new FillerLayoutBuilder();
    configure(builder);
    return builder.Build();
  }

  /// <summary>Column offsets from the centre out: <c>0, +1, -1, +2, -2, …</c> up to <paramref name="halfWidth"/>.</summary>
  private static IEnumerable<int> ColumnsCentreOut(int halfWidth) {
    yield return 0;
    for (int k = 1; k <= halfWidth; k++) {
      yield return k;
      yield return -k;
    }
  }

  /// <summary>
  /// Validates a footprint: no cell may sit at the principal origin (<c>0,0,0</c>) and no two cells may
  /// share a position. Throws <see cref="ArgumentException"/> on a violation, so an authoring mistake is a
  /// load-time crash rather than a silently broken structure.
  /// </summary>
  public static void Validate(IReadOnlyList<FillerCellSpec> cells) {
    var seen = new HashSet<(int, int, int)>();
    foreach (FillerCellSpec cell in cells) {
      if (cell.X == 0 && cell.Y == 0 && cell.Z == 0)
        throw new ArgumentException(
          "Filler footprint contains the principal origin (0,0,0); the principal already occupies it."
        );
      if (!seen.Add((cell.X, cell.Y, cell.Z)))
        throw new ArgumentException(
          $"Filler footprint has a duplicate cell at ({cell.X},{cell.Y},{cell.Z})."
        );
    }
  }
}
