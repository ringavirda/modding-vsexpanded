using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Code contributions to one catalogue, re-invoked after every load so a C# entry survives the clear
/// that precedes each <c>AssetsFinalize</c> read. Each catalogue registry exposes one of these as a
/// static <c>Contributors</c> property; the registry's own loader calls <see cref="Invoke"/> after its
/// JSON overlay, before anything reads the registry.
/// </summary>
public sealed class CatalogueContributors {
  private readonly List<Action<ICoreAPI>> _contributors = [];

  /// <summary>Registers a contributor, called with the api after every future
  /// <see cref="Invoke"/>. Call once from the mod's <c>Start</c>; calling twice runs it twice.</summary>
  public void Register(Action<ICoreAPI> contributor) {
    if (contributor != null)
      _contributors.Add(contributor);
  }

  /// <summary>Drops every registered contributor. For test isolation only; the game registers once per
  /// process.</summary>
  public void Clear() => _contributors.Clear();

  /// <summary>How many contributors are registered.</summary>
  public int Count => _contributors.Count;

  /// <summary>
  /// Runs every registered contributor against <paramref name="api"/>, in registration order. Called by
  /// the owning loader after its overlay. A contributor that throws is logged with its target type and
  /// skipped, so one bad C# contribution never costs the others theirs.
  /// </summary>
  public void Invoke(ICoreAPI api, ILogger logger) {
    foreach (Action<ICoreAPI> contributor in _contributors) {
      try {
        contributor(api);
      } catch (Exception e) {
        logger.Error(
          "[exlib] catalogue contributor {0} threw: {1}",
          contributor.Method.DeclaringType?.FullName ?? contributor.Method.Name,
          e.Message
        );
      }
    }
  }
}
