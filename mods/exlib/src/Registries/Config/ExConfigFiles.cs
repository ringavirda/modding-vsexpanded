using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries.Config;

/// <summary>
/// Shared helpers for the on-disk config files under the game's <c>ModConfig</c> folder. Used by the
/// bespoke per-player preferences store to carry a renamed file over instead of silently regenerating
/// defaults. The generic <see cref="ExConfigRegister{TConfig}"/> store does not rename: it folds
/// legacy per-mod files into the shared <see cref="ExConfigDocument"/> sections
/// (<see cref="ExConfigDocument.FoldLegacy"/>).
/// </summary>
public static class ExConfigFiles {
  /// <summary>
  /// If <paramref name="fileName"/> does not yet exist under <c>ModConfig</c> but one of
  /// <paramref name="legacyFileNames"/> does, renames that legacy file to the current name (first match
  /// wins). No-op when there are no legacy names, the new file already exists, or no legacy file is
  /// present; any IO failure is logged and swallowed (the caller then falls back to defaults).
  /// </summary>
  public static void RenameLegacy(
    ICoreAPI api,
    string modId,
    string fileName,
    IReadOnlyList<string> legacyFileNames
  ) {
    if (legacyFileNames == null || legacyFileNames.Count == 0)
      return;

    try {
      string dir = GamePaths.ModConfig;
      string target = Path.Combine(dir, fileName);
      if (File.Exists(target))
        return; // new file already present - leave any legacy file untouched.

      foreach (var legacy in legacyFileNames) {
        if (string.IsNullOrWhiteSpace(legacy))
          continue;
        string source = Path.Combine(dir, legacy);
        if (!File.Exists(source))
          continue;

        File.Move(source, target);
        api.Logger.Notification(
          "[{0}] Renamed legacy config '{1}' to '{2}'.",
          modId,
          legacy,
          fileName
        );
        return;
      }
    } catch (Exception e) {
      api.Logger.Warning(
        "[{0}] Could not migrate a legacy config file to '{1}'. {2}",
        modId,
        fileName,
        e
      );
    }
  }
}
