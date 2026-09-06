using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// The versioning contract every spec attribute carries. A parser reads the current form first and falls
/// back to the older ones, so a mod's content does not break on our schedule - and it refuses a form from
/// a newer build outright rather than mis-reading it as one it does know. The alternative, freezing at
/// release and going additive-only, was cheaper for us and worse for the people who asked for this.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public static class SpecSchema {
  /// <summary>The key a spec declares its version under.</summary>
  public const string Key = "schema";

  /// <summary>The first form, and what an undeclared <c>schema</c> means. Absent is not unversioned: the
  /// form that shipped before the field existed has a number whether or not it was written down.</summary>
  public const int First = 1;

  /// <summary>
  /// Reads the declared schema of <paramref name="node"/> against the <paramref name="current"/> one this
  /// build writes. An absent field reads as <see cref="First"/>; an older form is read as declared and is
  /// the caller's to fall back on; a newer one fails with a human-readable <paramref name="error"/> naming
  /// both versions, since the fix is on the reader's side.
  /// </summary>
  public static bool TryRead(
    JsonObject? node,
    int current,
    out int schema,
    out string? error
  ) {
    error = null;
    schema = node?[Key].AsInt(First) ?? First;

    if (schema < First) {
      error = $"'{Key}' must be at least {First} (was {schema})";
      return false;
    }
    if (schema > current) {
      error =
        $"declares {Key} {schema}, and this build reads up to {current}; update the library rather than "
        + "the declaration";
      return false;
    }
    return true;
  }
}
