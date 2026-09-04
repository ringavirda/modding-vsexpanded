using System.Collections.Generic;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Casting;

/// <summary>
/// Load-time validation of the mold patterns: every collectible carrying a
/// <see cref="MoldSpec.AttributeKey"/> attribute must parse into a valid <see cref="MoldSpec"/>. Run at
/// <c>AssetsFinalize</c> so a malformed <c>mold</c> block is a load-time complaint in the log rather
/// than a silent no-op at the casting cell. Pure over a collectible sequence, so it can be run headlessly.
/// </summary>
public static class PatternValidation {
  /// <summary>
  /// Returns one human-readable error per malformed pattern (empty when all valid). A pattern is any
  /// collectible that carries a <c>mold</c> attribute; collectibles without one are skipped (not patterns).
  /// </summary>
  public static List<string> Validate(
    IEnumerable<CollectibleObject> collectibles
  ) {
    var errors = new List<string>();
    foreach (CollectibleObject c in collectibles) {
      var mold = c?.Attributes?[MoldSpec.AttributeKey];
      if (mold is not { Exists: true })
        continue; // not a mold pattern
      if (!MoldSpec.TryParse(mold, out _, out string? error))
        errors.Add($"{c!.Code}: {error}");
    }
    return errors;
  }
}
