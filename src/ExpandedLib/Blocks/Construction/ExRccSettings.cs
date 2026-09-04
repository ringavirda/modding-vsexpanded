using System;
using System.Collections.Generic;

namespace ExpandedLib.Blocks.Construction;

/// <summary>
/// Player-tunable settings for the right-click construction system, supplied by each mod from its own
/// config. A mod registers a live getter keyed by its domain (mod id) and the shared
/// <see cref="ExRightClickConstructable"/> behaviour resolves the value at break time from the broken
/// block's <c>Code.Domain</c>, since exlib cannot reference a mod's <c>*Values</c> accessor directly.
/// The getter reads the config on every call, so a <c>/exmod config</c> change applies immediately.
/// exlib has a single runtime type identity, so this registry is shared across all dependent mods.
/// </summary>
public static class ExRccSettings {
  private static readonly Dictionary<string, Func<float>> _brokenDropsRatios =
    new();

  /// <summary>
  /// Registers the salvage fraction (0..1) for broken RCC mega-blocks of <paramref name="domain"/>:
  /// the share of the consumed construction materials scattered on break. Called at mod startup with
  /// the mod's config accessor, e.g. <c>() =&gt; IiexValues.RccBrokenDropsRatio</c>.
  /// </summary>
  public static void RegisterBrokenDropsRatio(
    string domain,
    Func<float> ratio
  ) => _brokenDropsRatios[domain] = ratio;

  /// <summary>
  /// The configured salvage fraction for <paramref name="domain"/>, or <c>null</c> when no mod
  /// registered one, in which case the behaviour keeps the JSON/default <c>brokenDropsRatio</c>.
  /// </summary>
  public static float? BrokenDropsRatio(string domain) =>
    _brokenDropsRatios.TryGetValue(domain, out Func<float>? getter)
      ? getter()
      : null;
}
