using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A single structure-local filler cell as declared in the <c>fillerOffsets</c>
/// JSON array: the offset from the principal (north orientation) and whether other
/// blocks are allowed to attach to the filler placed there. Attachment defaults to
/// <c>false</c> so mega-block footprints stay clean unless a cell opts in.
/// </summary>
public readonly record struct FillerOffset(Vec3i Offset, bool AllowAttach);

/// <summary>A resolved world-space filler cell carrying its per-cell attachment flag.</summary>
public readonly record struct FillerCell(BlockPos Pos, bool AllowAttach);

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
  /// filler at that cell rejects attached blocks).
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
          entry["allowAttach"].AsBool(false)
        )
      );
    }
    return result;
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
      cells.Add(
        new FillerCell(principalPos.AddCopy(r.X, r.Y, r.Z), off.AllowAttach)
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
