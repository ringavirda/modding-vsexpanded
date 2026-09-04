using Vintagestory.API.Common;

namespace ExpandedLib.Metals;

/// <summary>
/// A block entity holding molten metal that, once solidified and cooled, can be chipped out with a
/// chisel and hammer instead of breaking the whole block (canal cells, the molten barrel, the bessemer
/// charge). <see cref="MoltenChisel"/> drives the interaction - tool gating, the not-ready feedback,
/// the recovered drop, tool wear and sound - so an implementer supplies only the content-specific state
/// and the clear-and-recover step.
/// </summary>
public interface IChiselableMolten {
  /// <summary>
  /// Whether any solidified content is present. Decides whether a chisel and hammer click is claimed
  /// by the chisel-out, possibly reporting <see cref="ChiselBlockedError"/>, or falls through to the
  /// holder's other interactions. False when empty or still liquid.
  /// </summary>
  bool HasChiselableContent { get; }

  /// <summary>
  /// Whether the content can be chipped out now: solidified, cooled past the hardened threshold, and,
  /// where the holder caps it, small enough. False while it is still too hot to chip.
  /// </summary>
  bool CanChiselOut { get; }

  /// <summary>
  /// The <c>game:ingameerror-*</c> code to surface when <see cref="HasChiselableContent"/> is true but
  /// <see cref="CanChiselOut"/> is false, such as too hot or too full. <c>null</c> claims the click
  /// silently with no message.
  /// </summary>
  string? ChiselBlockedError { get; }

  /// <summary>
  /// Chips the hardened content out, clearing it from the holder, and returns the recovered metal-bit
  /// drop, or <c>null</c> when there is nothing to recover. Server-side, and called only once
  /// <see cref="CanChiselOut"/> has been confirmed.
  /// </summary>
  ItemStack? ChiselOut();
}
