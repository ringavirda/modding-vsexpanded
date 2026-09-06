using System;

namespace ExpandedLib.Blocks;

/// <summary>
/// Marks a field or auto-property of a block entity as saved state. <see cref="PersistScan"/> declares
/// it into the block entity's <c>State</c> without a hand-written <c>DeclareState</c> override; the key
/// defaults to the member name without a leading underscore, so <c>_temp</c> saves as <c>temp</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class PersistAttribute(string? key = null) : Attribute {
  /// <summary>The tree attribute name. Defaults to the member name with a leading underscore stripped.</summary>
  public string? Key { get; } = key;

  /// <summary>
  /// An older key read only when <see cref="Key"/> is absent from the tree - the fallback for a field
  /// renamed since it was first saved. Never written: a save under the new name never reintroduces the
  /// old one.
  /// </summary>
  public string? Legacy { get; init; }
}
