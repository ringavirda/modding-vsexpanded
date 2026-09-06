using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Catalogues;

/// <summary>
/// One catalogue's load outcome: files read, entries accepted, and the errors, each naming its asset.
/// Returned by every catalogue loader's <c>Load(ICoreAPI)</c>, so <c>AssetsFinalize</c> can log what
/// actually happened instead of only the failures.
/// </summary>
/// <param name="Catalogue">The catalogue's own name ("metals", "liquids", "processroutes"...).</param>
/// <param name="Files">Assets read under the catalogue's path, whether or not each parsed.</param>
/// <param name="Entries">Records accepted into the registry.</param>
/// <param name="Errors">One human-readable message per malformed file or clash, each naming the asset
/// it came from.</param>
public sealed record CatalogueLoadReport(
  string Catalogue,
  int Files,
  int Entries,
  IReadOnlyList<string> Errors
) {
  /// <summary>
  /// Writes one Notification line ("[exlib] metals: 3 file(s), 12 entr(ies), 0 error(s)") summarising
  /// the load, then one Error line per entry of <see cref="Errors"/>.
  /// </summary>
  public void Log(ILogger logger) {
    logger.Notification(
      "[exlib] {0}: {1} file(s), {2} entr(ies), {3} error(s)",
      Catalogue,
      Files,
      Entries,
      Errors.Count
    );
    foreach (string error in Errors)
      logger.Error("[exlib] " + error);
  }
}
