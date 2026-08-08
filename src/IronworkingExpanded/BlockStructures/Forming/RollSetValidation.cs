using System.Collections.Generic;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// Load-time validation of the <b>roll sets</b>: every collectible carrying a
/// <see cref="RollSetSpec.AttributeKey"/> attribute must parse into a valid <see cref="RollSetSpec"/>. Run once
/// at <c>AssetsFinalize</c> so a malformed <c>rollset</c> block is a complaint in the log rather than a silent
/// refusal at the mill - the same guarantee <see cref="Casting.PatternValidation"/> gives the casting patterns,
/// and it matters more here because a bad gap sequence fails by doing *nothing* when the player feeds stock in.
/// Pure over a collectible sequence, so it can be pinned headlessly.
/// </summary>
public static class RollSetValidation
{
  /// <summary>
  /// Returns one human-readable error per malformed roll set (empty when all valid). A roll set is any
  /// collectible carrying a <c>rollset</c> attribute; collectibles without one are skipped.
  /// </summary>
  public static List<string> Validate(IEnumerable<CollectibleObject> collectibles)
  {
    var errors = new List<string>();
    foreach (CollectibleObject c in collectibles)
    {
      var node = c?.Attributes?[RollSetSpec.AttributeKey];
      if (node is not { Exists: true })
        continue; // not a roll set
      if (!RollSetSpec.TryParse(node, out _, out string? error))
        errors.Add($"{c!.Code}: {error}");
    }
    return errors;
  }
}
