using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A single structure-local filler cell as declared in the <c>fillerOffsets</c>
/// JSON array: the offset from the principal (north orientation), whether other
/// blocks may attach to the filler placed there, and an optional set of per-cell
/// collision/selection boxes (north orientation) for footprint cells the mega-block
/// only partially fills, such as a slab. Attachment defaults to <c>false</c>;
/// <c>CollisionBoxes</c> is <c>null</c> for the common full-cube cell.
/// <see cref="PortFace"/>/<see cref="PortNetworkType"/> are both null, or both set, for a cell that
/// carries a passive network port (north orientation).
/// </summary>
public readonly record struct FillerOffset(
  Vec3i Offset,
  bool AllowAttach,
  Cuboidf[]? CollisionBoxes,
  FillerBehavior[]? Behaviors = null,
  string? PortFace = null,
  string? PortNetworkType = null
);

/// <summary>
/// A resolved world-space filler cell carrying its per-cell attachment flag and, when
/// the cell is only partially filled, its collision/selection boxes already rotated
/// into the placed orientation. <see cref="Behaviors"/> carry their connector face
/// already rotated to match, as does <see cref="PortFace"/> when the cell carries a
/// passive network port.
/// </summary>
public readonly record struct FillerCell(
  BlockPos Pos,
  bool AllowAttach,
  Cuboidf[]? CollisionBoxes,
  FillerBehavior[]? Behaviors = null,
  string? PortFace = null,
  string? PortNetworkType = null
);

/// <summary>
/// Helpers for the invisible mega-block footprint system. A mega-block occupies one grid cell
/// but renders across many; since collision resolves per cell, the surrounding cells are filled
/// with <see cref="BlockStructureFiller"/> placeholders that provide real collision and reroute
/// interaction/break/info to the principal.
/// </summary>
public static class StructureFillers {
  /// <summary>
  /// Asset code of the invisible filler block. <c>exlib</c> ships the one shared
  /// <c>structurefiller</c> block and points this at it; every dependent mod reuses it.
  /// Taken from the generated table rather than written as a literal: this code goes into the world
  /// for every footprint cell of every mega-block, and only a table reference is visible to the
  /// codegen drift test.
  /// </summary>
  public static AssetLocation FillerCode { get; set; } =
    new(ExlibBlocks.Structurefiller.Code);

  /// <summary>
  /// Parses an already-resolved <c>fillerOffsets</c> node (the principal's generated
  /// <see cref="IFillerHost.FillerOffsets"/> accessor) into north-orientation cells. Each entry is
  /// <c>{ x, y, z }</c> plus an optional <c>allowAttach</c> bool defaulting to <c>false</c> (the filler
  /// at that cell rejects attached blocks). A cell the mega-block only partially fills declares its
  /// solid volume with either <c>collisionBox</c> (one <c>{x1,y1,z1,x2,y2,z2}</c> cuboid) or
  /// <c>collisionBoxes</c> (an array of them); omit both for a full-cube cell. A cell may also declare
  /// a passive network port as <c>portFace</c> (a single-letter face code) plus <c>portNetwork</c>; both
  /// are omitted for a cell with no port.
  /// </summary>
  public static List<FillerOffset> ReadOffsets(JsonObject? offsetsNode) {
    var result = new List<FillerOffset>();
    if (offsetsNode == null || !offsetsNode.Exists)
      return result;

    foreach (var entry in offsetsNode.AsArray() ?? []) {
      result.Add(
        new FillerOffset(
          new Vec3i(entry["x"].AsInt(), entry["y"].AsInt(), entry["z"].AsInt()),
          entry["allowAttach"].AsBool(false),
          ReadBoxes(entry),
          ReadBehaviors(entry),
          entry["portFace"].AsString(),
          entry["portNetwork"].AsString()
        )
      );
    }
    return result;
  }

  /// <summary>
  /// Reads a cell's optional <c>behaviors</c> array: each entry is
  /// <c>{ "code": "&lt;registered class&gt;", "face": "&lt;north-orientation face&gt;"?, "properties": {…}? }</c>.
  /// The face is the connector direction in the block's north layout and is rotated into the placed
  /// orientation by <see cref="FootprintCells"/>; omit it for a behaviour that needs no connector.
  /// Returns null when the cell declares no behaviours.
  /// </summary>
  private static FillerBehavior[]? ReadBehaviors(JsonObject entry) {
    if (!entry["behaviors"].Exists)
      return null;
    var nodes = entry["behaviors"].AsArray();
    if (nodes == null || nodes.Length == 0)
      return null;

    var list = new List<FillerBehavior>(nodes.Length);
    foreach (var node in nodes) {
      string? code = node["code"].AsString();
      if (string.IsNullOrEmpty(code))
        continue;
      list.Add(
        new FillerBehavior(
          code,
          ParseFace(node["face"].AsString()),
          node["properties"].Exists ? node["properties"] : null
        )
      );
    }
    return list.Count > 0 ? [.. list] : null;
  }

  /// <summary>Resolves a face name ("north"/"n"…) to a <see cref="BlockFacing"/>, or null when absent.</summary>
  private static BlockFacing? ParseFace(string? face) =>
    string.IsNullOrEmpty(face)
      ? null
      : BlockFacing.FromCode(face) ?? BlockFacing.FromFirstLetter(face[0]);

  /// <summary>
  /// Reads a cell's optional partial-fill cuboids: <c>collisionBoxes</c> (array) takes precedence,
  /// else a single <c>collisionBox</c>, else <c>null</c> (full cube). Boxes are north orientation.
  /// </summary>
  private static Cuboidf[]? ReadBoxes(JsonObject entry) {
    if (entry["collisionBoxes"].Exists) {
      var nodes = entry["collisionBoxes"].AsArray();
      if (nodes == null || nodes.Length == 0)
        return null;
      var boxes = new List<Cuboidf>(nodes.Length);
      foreach (var node in nodes)
        if (node.AsObject<Cuboidf>() is { } box)
          boxes.Add(box);
      return boxes.Count > 0 ? [.. boxes] : null;
    }
    if (entry["collisionBox"].Exists)
      return entry["collisionBox"].AsObject<Cuboidf>() is { } box
        ? [box]
        : null;
    return null;
  }

  /// <summary>Resolves the world footprint cells for a principal block at <paramref name="principalPos"/>.</summary>
  public static List<FillerCell> FootprintCells(
    IFillerHost principal,
    BlockPos principalPos,
    int angle
  ) {
    var cells = new List<FillerCell>();
    foreach (var off in ReadOffsets(principal.FillerOffsets)) {
      Vec3i r = ExOrientation.RotateOffset(off.Offset, angle);
      // Partial boxes are declared in north orientation; rotate them into the placed
      // orientation around the cell centre (RotateBoxes pivots on 0.5,0.5,0.5).
      Cuboidf[]? boxes =
        off.CollisionBoxes == null
          ? null
          : ExOrientation.RotateBoxes(off.CollisionBoxes, angle);
      cells.Add(
        new FillerCell(
          principalPos.AddCopy(r.X, r.Y, r.Z),
          off.AllowAttach,
          boxes,
          RotateBehaviorFaces(off.Behaviors, angle),
          off.PortFace == null
            ? null
            : ExOrientation.RotateSideWord(off.PortFace, angle),
          off.PortNetworkType
        )
      );
    }
    return cells;
  }

  /// <summary>
  /// Rotates each declared behaviour's north-orientation connector face into the placed orientation
  /// (the behaviour's other config is orientation-independent and passes through unchanged). Returns
  /// the same array reference when there is nothing to rotate.
  /// </summary>
  private static FillerBehavior[]? RotateBehaviorFaces(
    FillerBehavior[]? behaviors,
    int angle
  ) {
    if (behaviors == null || behaviors.Length == 0)
      return behaviors;
    var rotated = new FillerBehavior[behaviors.Length];
    for (int i = 0; i < behaviors.Length; i++) {
      FillerBehavior b = behaviors[i];
      rotated[i] =
        b.ConnectorFace == null
          ? b
          : b with {
            ConnectorFace = ExOrientation.RotateFacing(b.ConnectorFace, angle),
          };
    }
    return rotated;
  }

  /// <summary>True when every cell is free (air or replaceable) so fillers can be placed.</summary>
  public static bool CanPlace(
    IWorldAccessor world,
    IEnumerable<FillerCell> cells
  ) {
    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return false;
    foreach (var cell in cells) {
      Block existing = world.BlockAccessor.GetBlock(cell.Pos);
      if (existing.Id != 0 && !existing.IsReplacableBy(filler))
        return false;
    }
    return true;
  }

  /// <summary>Places filler blocks at every cell and links each to the principal. Server-side only.</summary>
  public static void PlaceFillers(
    IWorldAccessor world,
    BlockPos principalPos,
    IEnumerable<FillerCell> cells
  ) {
    if (world.Side != EnumAppSide.Server)
      return;

    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return;

    foreach (var cell in cells) {
      world.BlockAccessor.SetBlock(filler.BlockId, cell.Pos);
      if (
        world.BlockAccessor.GetBlockEntity(cell.Pos)
        is BlockEntityStructureFiller be
      ) {
        be.Principal = principalPos.Copy();
        be.AllowAttach = cell.AllowAttach;
        be.CollisionBoxes = cell.CollisionBoxes;
        be.PortFace = cell.PortFace;
        be.PortNetworkType = cell.PortNetworkType;
        // Stores and (re)creates the hosted behaviours now that the principal link is set, so an MP
        // port joins the network at placement rather than at the next reload.
        be.SetHostedBehaviors(cell.Behaviors);
        be.MarkDirty(true);
      }
    }
  }

  /// <summary>
  /// Clears the structure's filler cells. Only removes a cell when it actually
  /// holds a filler linked to <paramref name="principalPos"/>, so a neighbouring
  /// structure's fillers are never disturbed.
  /// </summary>
  public static void RemoveFillers(
    IWorldAccessor world,
    BlockPos principalPos,
    IEnumerable<FillerCell> cells
  ) {
    if (world.Side != EnumAppSide.Server)
      return;

    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return;

    foreach (var cell in cells) {
      if (world.BlockAccessor.GetBlock(cell.Pos).Id != filler.BlockId)
        continue;
      if (
        world.BlockAccessor.GetBlockEntity(cell.Pos)
          is BlockEntityStructureFiller be
        && be.Principal != null
        && be.Principal.Equals(principalPos)
      )
        world.BlockAccessor.SetBlock(0, cell.Pos);
    }
  }
}
