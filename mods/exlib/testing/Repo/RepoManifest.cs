using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Reads the repo manifest, <c>exmod.json</c>, and exposes the layout it declares: which folder is
/// which mod's, which folder is which sample's (and where its tests live), which test projects belong
/// to neither, and which mod's tree carries another domain's overlay (<c>"overlays"</c> on a mod's own
/// entry).
/// <para>
/// Absent the file - a checkout from before the manifest existed, or the harness running against a
/// third party's repo - the default layout applies: every directory under <c>mods/</c> is a mod named
/// after itself, no samples, no extra tests, and <c>game</c> overlaid onto <c>iiex</c> only when its
/// tree exists on disk. Read fresh on every access rather than cached, the same choice
/// <see cref="DefinitionGoldens.RepoRoot"/> makes, since <see cref="DefinitionGoldens.RepoRootOverride"/>
/// can move the root a caller resolves against mid-process.
/// </para>
/// </summary>
public static class RepoManifest {
  /// <summary>One sample's project path and test project path.</summary>
  public readonly record struct SampleEntry(string Path, string Tests);

  /// <summary>Every mod's id and absolute path, in the manifest's declared order (default layout:
  /// directory order under <c>mods/</c>).</summary>
  public static IReadOnlyDictionary<string, string> Mods => Load().Mods;

  /// <summary>Every sample's id, project path and test project path, in the manifest's declared order.
  /// Empty in the default layout - a sample exists here only once <c>exmod.json</c> names it.</summary>
  public static IReadOnlyDictionary<string, SampleEntry> Samples =>
    Load().Samples;

  /// <summary>Test projects belonging to neither a mod nor a sample, as absolute paths. Empty in the
  /// default layout.</summary>
  public static IReadOnlyList<string> Tests => Load().Tests;

  /// <summary>An overlay domain mapped to the id of the mod whose tree ships it (<c>game</c> -&gt;
  /// <c>iiex</c>, from that mod's own <c>"overlays"</c> object). In the default layout this is exactly
  /// that one rule, and only when <c>mods/iiex/assets/game</c> exists.</summary>
  public static IReadOnlyDictionary<string, string> Overlays => Load().Overlays;

  private sealed record Manifest(
    IReadOnlyDictionary<string, string> Mods,
    IReadOnlyDictionary<string, SampleEntry> Samples,
    IReadOnlyList<string> Tests,
    IReadOnlyDictionary<string, string> Overlays
  );

  private static Manifest Load() {
    string root = DefinitionGoldens.RepoRoot();
    string manifestPath = Path.Combine(root, "exmod.json");
    return File.Exists(manifestPath)
      ? Parse(root, manifestPath)
      : Default(root);
  }

  private static Manifest Parse(string root, string manifestPath) {
    var doc = JObject.Parse(File.ReadAllText(manifestPath));
    var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var overlays = new Dictionary<string, string>(
      StringComparer.OrdinalIgnoreCase
    );

    if (doc["mods"] is JObject modsObj) {
      foreach (JProperty entry in modsObj.Properties()) {
        mods[entry.Name] = Resolve(root, (string)entry.Value["path"]!);
        if (entry.Value["overlays"] is JObject modOverlays)
          foreach (JProperty overlay in modOverlays.Properties())
            overlays[overlay.Name] = (string)overlay.Value!;
      }
    }

    var samples = new Dictionary<string, SampleEntry>(
      StringComparer.OrdinalIgnoreCase
    );
    if (doc["samples"] is JObject samplesObj) {
      foreach (JProperty entry in samplesObj.Properties())
        samples[entry.Name] = new SampleEntry(
          Resolve(root, (string)entry.Value["path"]!),
          Resolve(root, (string)entry.Value["tests"]!)
        );
    }

    List<string> tests = doc["tests"] is JArray testsArr
      ? [.. testsArr.Select(t => Resolve(root, (string)t!))]
      : [];

    return new Manifest(mods, samples, tests, overlays);
  }

  private static Manifest Default(string root) {
    string modsRoot = Path.Combine(root, "mods");
    var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (Directory.Exists(modsRoot))
      foreach (string dir in Directory.EnumerateDirectories(modsRoot))
        mods[new DirectoryInfo(dir).Name] = dir;

    var overlays = new Dictionary<string, string>(
      StringComparer.OrdinalIgnoreCase
    );
    if (
      mods.TryGetValue("iiex", out string? iiexPath)
      && Directory.Exists(Path.Combine(iiexPath, "assets", "game"))
    )
      overlays["game"] = "iiex";

    return new Manifest(
      mods,
      new Dictionary<string, SampleEntry>(StringComparer.OrdinalIgnoreCase),
      [],
      overlays
    );
  }

  // exmod.json spells every path with '/', repo-relative from its own location at the repo root.
  private static string Resolve(string root, string repoRelativePath) =>
    Path.Combine(
      root,
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );
}
