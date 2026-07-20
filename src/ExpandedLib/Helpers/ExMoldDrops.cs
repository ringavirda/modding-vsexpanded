using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Reads a tool mold's cast-product templates off its block attributes: the vanilla <c>drop</c> (single)
/// / <c>drops</c> (array) schema, resolved with the mold's OWN domain as the default for unqualified
/// codes - byte-for-byte what <c>BlockEntityToolMold.GetMoldedStacks</c> does.
/// <para>
/// Shared rather than copied because two mods read the same attributes for two different questions: smex
/// asks "can this metal be cast here at all" (the interact gate) and iwex asks "what does this metal
/// actually cast into" (the cast-domain patch, for a metal with no game-domain product). Getting the
/// <c>drop</c>-else-<c>drops</c> precedence or the default domain wrong in one of them silently breaks
/// casting, so the read lives in one place.
/// </para>
/// <para>
/// The returned templates are freshly deserialized on every call and safe to mutate - which matters,
/// because resolving one substitutes <c>{metal}</c> into its <see cref="JsonItemStack.Code"/> in place.
/// </para>
/// </summary>
public static class ExMoldDrops
{
  /// <summary>The mold's drop templates, empty when it declares none.</summary>
  public static List<JsonItemStack> Templates(Block? mold)
  {
    var templates = new List<JsonItemStack>();
    if (mold?.Attributes == null)
      return templates;

    // Vanilla treats the two keys as exclusive: a mold with `drop` never consults `drops`.
    if (mold.Attributes["drop"].Exists)
    {
      JsonItemStack? one = mold
        .Attributes["drop"]
        .AsObject<JsonItemStack>(null, mold.Code.Domain);
      if (one != null)
        templates.Add(one);
      return templates;
    }

    JsonItemStack[]? many = mold
      .Attributes["drops"]
      .AsObject<JsonItemStack[]>(null, mold.Code.Domain);
    if (many != null)
      templates.AddRange(many);
    return templates;
  }
}
