using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A single structure-local filler cell as declared in the <c>fillerOffsets</c>
/// JSON array: the offset from the principal (north orientation), whether other
/// blocks are allowed to attach to the filler placed there, and an optional set of
/// per-cell collision/selection boxes (north orientation) for footprint cells the
/// mega-block only partially fills (e.g. a slab). Attachment defaults to <c>false</c>
/// so mega-block footprints stay clean unless a cell opts in; <c>CollisionBoxes</c>
/// is <c>null</c> for the common full-cube cell.
/// </summary>
public readonly record struct FillerOffset(
  Vec3i Offset,
  bool AllowAttach,
  Cuboidf[]? CollisionBoxes
);

/// <summary>
/// A resolved world-space filler cell carrying its per-cell attachment flag and, when
/// the cell is only partially filled, its collision/selection boxes already rotated
/// into the placed orientation.
/// </summary>
public readonly record struct FillerCell(
  BlockPos Pos,
  bool AllowAttach,
  Cuboidf[]? CollisionBoxes
);

/// <summary>
/// Shared helper for the invisible mega-block footprint system. A mega-block occupies one grid
/// cell but renders across many; since collision resolves per cell, the surrounding cells are
/// filled with <see cref="BlockStructureFiller"/> placeholders that provide real collision and
/// reroute interaction/break/info to the principal.
/// </summary>
public static class StructureFillers
{
  /// <summary>
  /// Asset code of the invisible filler block. <c>exlib</c> ships the one shared
  /// <c>structurefiller</c> block and points this at it; every dependent mod reuses it.
  /// </summary>
  public static AssetLocation FillerCode { get; set; } =
    new("exlib:structurefiller");

  /// <summary>
  /// Parses an already-resolved <c>fillerOffsets</c> node (the principal's generated
  /// <see cref="IFillerHost.FillerOffsets"/> accessor) into north-orientation cells. Each entry is
  /// <c>{ x, y, z }</c> plus an optional <c>allowAttach</c> bool that defaults to <c>false</c> (the
  /// filler at that cell rejects attached blocks). A cell that the mega-block only partially fills
  /// can declare its solid volume with either <c>collisionBox</c> (one <c>{x1,y1,z1,x2,y2,z2}</c>
  /// cuboid) or <c>collisionBoxes</c> (an array of them); omit both for a full-cube cell.
  /// </summary>
  public static List<FillerOffset> ReadOffsets(JsonObject? offsetsNode)
  {
    var result = new List<FillerOffset>();
    if (offsetsNode == null || !offsetsNode.Exists)
      return result;

    foreach (var entry in offsetsNode.AsArray() ?? [])
    {
      result.Add(
        new FillerOffset(
          new Vec3i(entry["x"].AsInt(), entry["y"].AsInt(), entry["z"].AsInt()),
          entry["allowAttach"].AsBool(false),
          ReadBoxes(entry)
        )
      );
    }
    return result;
  }

  /// <summary>
  /// Reads a cell's optional partial-fill cuboids: <c>collisionBoxes</c> (array) takes precedence,
  /// else a single <c>collisionBox</c>, else <c>null</c> (full cube). Boxes are north orientation.
  /// </summary>
  private static Cuboidf[]? ReadBoxes(JsonObject entry)
  {
    if (entry["collisionBoxes"].Exists)
    {
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
  )
  {
    var cells = new List<FillerCell>();
    foreach (var off in ReadOffsets(principal.FillerOffsets))
    {
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
          boxes
        )
      );
    }
    return cells;
  }

  /// <summary>True when every cell is free (air or replaceable) so fillers can be placed.</summary>
  public static bool CanPlace(
    IWorldAccessor world,
    IEnumerable<FillerCell> cells
  )
  {
    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return false;
    foreach (var cell in cells)
    {
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
  )
  {
    if (world.Side != EnumAppSide.Server)
      return;

    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return;

    foreach (var cell in cells)
    {
      world.BlockAccessor.SetBlock(filler.BlockId, cell.Pos);
      if (
        world.BlockAccessor.GetBlockEntity(cell.Pos)
        is BlockEntityStructureFiller be
      )
      {
        be.Principal = principalPos.Copy();
        be.AllowAttach = cell.AllowAttach;
        be.CollisionBoxes = cell.CollisionBoxes;
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
  )
  {
    if (world.Side != EnumAppSide.Server)
      return;

    Block? filler = world.GetBlock(FillerCode);
    if (filler == null)
      return;

    foreach (var cell in cells)
    {
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
