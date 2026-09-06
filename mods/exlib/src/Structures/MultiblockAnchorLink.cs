using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Structures;

/// <summary>
/// A throttled resolver a functional component (a molten tap, a charging hopper, a tuyere) keeps to
/// find and keep reading the multiblock anchor it belongs to. The anchor pushes to its components by
/// structure-local offset and nothing points back, so the component scans a bounded box for the anchor
/// (<see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>). That scan is the expensive part
/// and <c>GetBlockInfo</c> runs every frame the player looks at the block, so it re-runs at most once a
/// second; a resolved anchor is cached and re-validated cheaply on each call (one block-entity lookup
/// plus one ownership check). When the anchor moves or the structure breaks, the cache clears and the
/// throttled re-scan takes over.
/// </summary>
public sealed class MultiblockAnchorLink<T>
  where T : BlockEntityMultiblockStructure {
  private readonly BlockEntity _component;
  private readonly int _horizontal;
  private readonly int _below;
  private readonly int _above;

  private BlockPos? _anchorPos;
  private long _lastScanMs = -1;

  private const long RescanIntervalMs = 1000;

  /// <param name="component">The functional block whose anchor this resolves. Its position is the scan
  /// origin, its <c>Api.World</c> the scan surface.</param>
  /// <param name="horizontal">Cells to scan out on each horizontal axis.</param>
  /// <param name="below">Cells to scan downward (components sit above their anchor).</param>
  /// <param name="above">Cells to scan upward (a small margin).</param>
  public MultiblockAnchorLink(
    BlockEntity component,
    int horizontal,
    int below,
    int above
  ) {
    _component = component;
    _horizontal = horizontal;
    _below = below;
    _above = above;
  }

  /// <summary>
  /// The owning anchor, or null when none is in range. A still-valid cached anchor is read live; an
  /// empty or invalidated cache triggers a bounded re-scan, at most once per
  /// <see cref="RescanIntervalMs"/>, so a component with no anchor does not scan every frame.
  /// </summary>
  public T? Resolve() {
    IWorldAccessor? world = _component.Api?.World;
    if (world == null)
      return null;

    // Re-validate the cached anchor cheaply: it is still ours only if a T still sits at that cell and
    // still owns us (it may have been broken, or replaced by an unrelated block).
    if (_anchorPos != null) {
      if (
        world.BlockAccessor.GetBlockEntity(_anchorPos) is T cached
        && cached.OwnsCell(_component.Pos)
      )
        return cached;
      _anchorPos = null;
    }

    long now = world.ElapsedMilliseconds;
    if (_lastScanMs >= 0 && now - _lastScanMs < RescanIntervalMs)
      return null;
    _lastScanMs = now;

    T? found = BlockEntityMultiblockStructure.FindAnchorOwning<T>(
      world,
      _component.Pos,
      _horizontal,
      _below,
      _above
    );
    _anchorPos = found?.Pos;
    return found;
  }
}
