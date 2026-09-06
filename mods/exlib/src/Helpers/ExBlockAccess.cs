using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Helpers;

/// <summary>
/// The block-entity lookup every machine writes by hand: <c>GetBlockEntity(pos) is X be</c> with a
/// null guard, and the same one step over to a neighbour. Pure convenience over
/// <see cref="IBlockAccessor.GetBlockEntity(BlockPos)"/>; never mutates, never throws.
/// </summary>
public static class ExBlockAccess {
  /// <summary>The block entity of type <typeparamref name="T"/> at <paramref name="pos"/>, or
  /// <c>null</c> when there is none, it is a different type, or the chunk is unloaded.</summary>
  public static T? BlockEntity<T>(this IBlockAccessor accessor, BlockPos pos)
    where T : class => accessor.GetBlockEntity(pos) as T;

  /// <summary>Same lookup as <see cref="BlockEntity{T}"/>, as a <c>TryGet</c> for a guard clause.</summary>
  public static bool TryGetBlockEntity<T>(
    this IBlockAccessor accessor,
    BlockPos pos,
    [NotNullWhen(true)] out T? be
  )
    where T : class {
    be = accessor.BlockEntity<T>(pos);
    return be != null;
  }

  /// <summary>The block entity of type <typeparamref name="T"/> one step from <paramref name="pos"/>
  /// in <paramref name="facing"/>'s direction, or <c>null</c>.</summary>
  public static T? Neighbour<T>(
    this IBlockAccessor accessor,
    BlockPos pos,
    BlockFacing facing
  )
    where T : class => accessor.BlockEntity<T>(pos.AddCopy(facing));

  /// <summary>Every (facing, block entity) pair of type <typeparamref name="T"/> around
  /// <paramref name="pos"/>, walking <paramref name="facings"/> (<see cref="BlockFacing.ALLFACES"/>
  /// when <c>null</c>). Only matching neighbours are yielded, so an empty result means none of the
  /// walked faces carry one.</summary>
  public static IEnumerable<(BlockFacing Facing, T Entity)> Neighbours<T>(
    this IBlockAccessor accessor,
    BlockPos pos,
    IEnumerable<BlockFacing>? facings = null
  )
    where T : class {
    foreach (BlockFacing facing in facings ?? BlockFacing.ALLFACES) {
      T? entity = accessor.Neighbour<T>(pos, facing);
      if (entity != null)
        yield return (facing, entity);
    }
  }
}
