using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Verify;

/// <summary>
/// One loaded mod folder or zip: its <see cref="ModId"/> (read from <c>modinfo.json</c>) and the
/// directory holding its <c>assets/</c> tree. A zip is extracted once into a temp directory so the
/// rest of the tool only ever reads from disk, the same shape a folder mod already has.
/// </summary>
public sealed class ModSource {
  /// <summary>The mod id from <c>modinfo.json</c>'s <c>modid</c> field.</summary>
  public string ModId { get; }

  /// <summary>The directory directly containing <c>modinfo.json</c> and <c>assets/</c>.</summary>
  public string RootDir { get; }

  /// <summary>The original path this source was loaded from - a folder or a zip file - used only
  /// for messages, never for reading.</summary>
  public string OriginalPath { get; }

  private ModSource(string modId, string rootDir, string originalPath) {
    ModId = modId;
    RootDir = rootDir;
    OriginalPath = originalPath;
  }

  /// <summary>
  /// Loads a mod from a folder (<paramref name="path"/> containing <c>modinfo.json</c> directly)
  /// or a zip (extracted into a fresh temp directory, cleaned up by the OS's temp housekeeping
  /// rather than by this tool - a one-shot CLI run has no teardown hook worth writing).
  /// </summary>
  /// <exception cref="FileNotFoundException"><paramref name="path"/> is neither an existing folder
  /// nor an existing file.</exception>
  /// <exception cref="InvalidDataException"><c>modinfo.json</c> is missing or has no <c>modid</c>.</exception>
  public static ModSource Load(string path) {
    string root;
    if (Directory.Exists(path)) {
      root = Path.GetFullPath(path);
    } else if (File.Exists(path)) {
      string extractTo = Path.Combine(
        Path.GetTempPath(),
        "exlib-verify-" + Guid.NewGuid().ToString("N")
      );
      Directory.CreateDirectory(extractTo);
      ZipFile.ExtractToDirectory(path, extractTo);
      // A mod zip sometimes wraps its content one folder deep (GitHub's "download zip" habit); if
      // modinfo.json isn't at the extraction root, use the sole subfolder that has one.
      root = File.Exists(Path.Combine(extractTo, "modinfo.json"))
        ? extractTo
        : FindModinfoDir(extractTo) ?? extractTo;
    } else {
      throw new FileNotFoundException($"No such mod folder or zip: {path}");
    }

    string modinfoPath = Path.Combine(root, "modinfo.json");
    if (!File.Exists(modinfoPath))
      throw new InvalidDataException($"{path}: no modinfo.json found");

    var modinfo = JObject.Parse(File.ReadAllText(modinfoPath));
    string? modId = (string?)modinfo["modid"];
    if (string.IsNullOrEmpty(modId))
      throw new InvalidDataException($"{modinfoPath}: no 'modid' field");

    return new ModSource(modId, root, path);
  }

  private static string? FindModinfoDir(string extractedRoot) {
    foreach (string dir in Directory.EnumerateDirectories(extractedRoot))
      if (File.Exists(Path.Combine(dir, "modinfo.json")))
        return dir;
    return null;
  }
}
