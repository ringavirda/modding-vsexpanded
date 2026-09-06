using System;
using System.IO;

namespace ExpandedLib.Verify;

/// <summary>Locates the Vintage Story install this run checks against, mirroring
/// <c>ExpandedLib.Testing.VsAssemblyResolver</c>'s own priority (an explicit override, then the
/// in-repo provisioned install) without depending on that harness project - this tool ships
/// standalone, run against a mod folder with no repository around it at all.</summary>
public static class GameInstall {
  /// <summary>
  /// Resolves the game path: <paramref name="explicitPath"/> if given, else the
  /// <c>VINTAGE_STORY</c> environment variable, else a provisioned <c>.game/1.22-server</c> or
  /// <c>.game/1.22</c> found by walking up from the current directory.
  /// </summary>
  /// <exception cref="DirectoryNotFoundException">None of the above yields an install carrying
  /// <c>VintagestoryAPI.dll</c>.</exception>
  public static string Resolve(string? explicitPath) {
    if (!string.IsNullOrEmpty(explicitPath)) {
      if (!File.Exists(Path.Combine(explicitPath, "VintagestoryAPI.dll")))
        throw new DirectoryNotFoundException($"--game {explicitPath}: no VintagestoryAPI.dll there");
      return Path.GetFullPath(explicitPath);
    }

    string? env = Environment.GetEnvironmentVariable("VINTAGE_STORY");
    if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "VintagestoryAPI.dll")))
      return Path.GetFullPath(env);

    for (DirectoryInfo? dir = new(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent) {
      foreach (string slug in new[] { "1.22-server", "1.22" }) {
        string candidate = Path.Combine(dir.FullName, ".game", slug);
        if (File.Exists(Path.Combine(candidate, "VintagestoryAPI.dll")))
          return candidate;
      }
    }

    throw new DirectoryNotFoundException(
      "No Vintage Story install found - pass --game <path>, set VINTAGE_STORY, or run from inside "
        + "a checkout with .game/1.22(-server) provisioned."
    );
  }
}
