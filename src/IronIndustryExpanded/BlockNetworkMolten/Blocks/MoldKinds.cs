using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// Classifies tool molds by where they can be cast: small molds sit on the
/// mold pedestal, large molds (anvil, helve hammer) are cast in the canal tap.
/// </summary>
public static class MoldKinds {
  /// <summary>Tool-mold types too large for the pedestal; cast in the canal tap instead.</summary>
  public static readonly HashSet<string> LargeToolTypes =
  [
    "helvehammer",
    "anvil",
  ];

  /// <summary>True when <paramref name="block"/> is a large tool mold, castable in the canal tap only.</summary>
  public static bool IsLarge(Block? block) =>
    block is BlockToolMold
    && LargeToolTypes.Contains(block.Variant["tooltype"] ?? "");

  /// <summary>True when <paramref name="block"/> is a small tool mold that fits the mold pedestal.</summary>
  public static bool FitsPedestal(Block? block) =>
    block is BlockToolMold
    && !LargeToolTypes.Contains(block.Variant["tooltype"] ?? "");
}
