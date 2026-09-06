using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Structures;

/// <summary>
/// Concrete <see cref="BlockEntityMultiblockStructure"/> for a JSON-only mega-block: no C# subclass is
/// needed to get orientation, completion monitoring and the incomplete/complete messages.
/// </summary>
/// <remarks>
/// Orientation follows the block's own <c>side</c> (full word) or <c>orientation</c> (single letter)
/// variant, the same rule <see cref="ExpandedLib.Blocks.BlockBehaviorExOrientable"/> applies to placement.
/// The messages are the domain's own <c>multiblock-&lt;blockpath&gt;-incomplete</c>/<c>-complete</c> lang
/// keys when the domain declares them, falling back to <c>exlib:multiblock-incomplete</c>/<c>-complete</c>
/// otherwise, so a modder who never writes either key still gets a readable message.
/// </remarks>
[BlockEntityRegister("ExMultiblock", PrefixModId = false)]
public class BlockEntityMultiblock : BlockEntityMultiblockStructure {
  /// <inheritdoc/>
  protected override void UpdateStructureRotation() {
    if (Block == null)
      return;
    SetStructureAngle(
      ExOrientation.AngleFromSide(
        Block.Variant?["side"] ?? Block.Variant?["orientation"]
      )
    );
  }

  /// <inheritdoc/>
  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.GetWithFallback(DomainKey("incomplete"), FallbackKey("incomplete"), missingCount);

  /// <inheritdoc/>
  protected override string GetCompleteMessage() =>
    Lang.GetWithFallback(DomainKey("complete"), FallbackKey("complete"));

  /// <summary>Test seam: <see cref="GetIncompleteMessage"/> is only reached from a client-side
  /// <c>Interact</c> in production, which a headless test has no client API to drive.</summary>
  internal string IncompleteMessageForTest(int missingCount) =>
    GetIncompleteMessage(missingCount);

  // "<domain>:multiblock-<blockpath>-<suffix>" - the key a domain declares for its own block to override
  // the exlib default with wording naming the machine.
  private string DomainKey(string suffix) =>
    $"{Block.Code.Domain}:multiblock-{Block.Code.Path}-{suffix}";

  // The exlib default, requested when the domain declares no key of its own.
  private static string FallbackKey(string suffix) => $"exlib:multiblock-{suffix}";
}
