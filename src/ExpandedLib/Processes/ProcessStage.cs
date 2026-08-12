using System;
using System.Linq;

namespace ExpandedLib.Processes;

/// <summary>
/// One state of a piece part-way through a sequence process: the thickness it sits at, the shape element
/// that draws it, the machine families that accept it, and the item code it becomes when it is a stopping
/// point. A stage is addressed by (thickness, accepting family) rather than by thickness alone, because a
/// fork means two families draw different geometry at the same gauge.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
/// <param name="Thickness">The gauge this state sits at, in block-space units.</param>
/// <param name="Element">Shape element drawing this state, or null when the whole shape file is the stage.</param>
/// <param name="AcceptedBy">Machine families that take this state. A stage nothing accepts is unreachable.</param>
/// <param name="Code">The item this state becomes when claimed, or null for a render-only intermediate.</param>
/// <param name="Generate">Whether an item is built for <paramref name="Code"/>. False means the code exists already: wire it up, build nothing.</param>
/// <param name="FormerCodes">Codes this stage's product used to have, so the rename gets its stack migration free.</param>
public sealed record ProcessStage(
  float Thickness,
  string? Element,
  string[] AcceptedBy,
  string? Code,
  bool Generate = true,
  string[]? FormerCodes = null
) {
  /// <summary>Codes this product used to have. Empty rather than null, so a caller never has to check.</summary>
  public string[] FormerCodes { get; init; } = FormerCodes ?? [];

  /// <summary>Whether the piece can be claimed here. A stage naming a code is where the ladder may be left
  /// with a finished item; one without is a state the piece passes through.</summary>
  public bool IsStoppingPoint => !string.IsNullOrWhiteSpace(Code);

  /// <summary>Whether <paramref name="family"/> takes this state.</summary>
  public bool IsAcceptedBy(string? family) =>
    family != null
    && AcceptedBy.Contains(family, StringComparer.OrdinalIgnoreCase);
}
