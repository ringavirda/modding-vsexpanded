using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ExpandedLib.Testing;

/// <summary>
/// Parses every JSON asset under one shipped tree. The game reports a syntax error only to the server
/// log and then carries on with the file's whole contents missing, so a corrupt recipe or shape file
/// removes everything in it while the build and the rest of the suite stay green.
/// <para>
/// Every patch entry must also declare the side it runs on: <c>JsonPatch.Side</c> defaults to
/// <c>Universal</c>, not to the target file's category, so an entry with no <c>"side"</c> is evaluated
/// on the client too - where blocktypes, itemtypes and recipes do not exist. The patch is then counted
/// as not-found and logs a miss per entry on every client start, and the engine's own loader comments
/// on exactly this case as the reason it does not warn about it. Never write <c>"side": null</c> either
/// - that takes the branch which skips the patch on BOTH sides, silently and with no log line at all.
/// </para>
/// </summary>
public static class ShippedJson {
  // blocktypes, itemtypes and recipes are all EnumAppSide.Server asset categories. Anything else
  // (shapes, textures, lang) is legitimately client-side or universal, so only the side's presence is
  // required there.
  private static readonly string[] ServerOnlyCategories = ["blocktypes", "itemtypes", "recipes"];

  /// <summary>Every JSON asset under <paramref name="assetTree"/>: parses, carries no control character
  /// outside tab/LF/CR (with the offending offsets named - invisible in an editor, survives copy/paste,
  /// and breaks the parse at a column the error message cannot show), and, for a file under
  /// <c>patches/</c>, every entry declares its side (a server-only category must declare
  /// <c>"Server"</c>). Empty means clean.</summary>
  public static IReadOnlyList<string> Check(string assetTree) {
    var offenders = new List<string>();
    foreach (string relative in AssetFiles(assetTree)) {
      byte[] bytes = File.ReadAllBytes(FullPath(relative));

      var badOffsets = new List<string>();
      for (int i = 0; i < bytes.Length; i++) {
        byte b = bytes[i];
        if (b < 0x20 && b != 0x09 && b != 0x0a && b != 0x0d)
          badOffsets.Add($"0x{b:x2} at offset {i}");
      }
      if (badOffsets.Count > 0)
        offenders.Add(
          $"{relative} contains {badOffsets.Count} control character(s): "
            + string.Join(", ", badOffsets.Take(8))
        );

      JsonDocument doc;
      try {
        doc = Parse(bytes);
      } catch (Exception ex) {
        offenders.Add($"{relative} is not valid JSON: {ex.Message}");
        continue;
      }
      using (doc) {
        if (!IsPatchFile(relative))
          continue;
        int index = 0;
        foreach (JsonElement entry in doc.RootElement.EnumerateArray()) {
          if (
            !entry.TryGetProperty("side", out JsonElement side)
            || side.ValueKind != JsonValueKind.String
          )
            offenders.Add($"{relative} [{index}] declares no side");
          else if (ServerOnlyCategory(entry) && side.GetString() != "Server")
            offenders.Add(
              $"{relative} [{index}] targets a server-only category but declares "
                + $"\"{side.GetString()}\""
            );
          index++;
        }
      }
    }
    return offenders;
  }

  /// <summary>The patch files under <paramref name="assetTree"/> - the premise a caller asserts is
  /// non-empty where the tree is known to carry a <c>patches/</c> folder, since a renamed or moved
  /// folder would otherwise make <see cref="Check"/>'s patch rule pass trivially.</summary>
  public static IReadOnlyList<string> PatchFiles(string assetTree) =>
    [.. AssetFiles(assetTree).Where(IsPatchFile)];

  private static bool IsPatchFile(string relativePath) =>
    relativePath.Contains("/patches/", StringComparison.Ordinal);

  private static bool ServerOnlyCategory(JsonElement entry) =>
    entry.TryGetProperty("file", out JsonElement file)
    && file.GetString() is { } target
    && ServerOnlyCategories.Contains(target.Split(':').Last().Split('/').First());

  private static JsonDocument Parse(byte[] utf8Json) =>
    JsonDocument.Parse(
      utf8Json,
      new JsonDocumentOptions {
        // The game's loader tolerates both, so the guard must too - otherwise it would fail files
        // that ship and work.
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );

  private static string FullPath(string repoRelativePath) =>
    Path.Combine(
      RepoPaths.Root,
      repoRelativePath.Replace('/', Path.DirectorySeparatorChar)
    );

  // Every JSON under the tree, repo-relative and forward-slashed so a finding reads the same on any
  // platform.
  private static IEnumerable<string> AssetFiles(string assetTree) {
    if (!Directory.Exists(assetTree))
      yield break;
    string root = RepoPaths.Root;
    foreach (
      string file in Directory.EnumerateFiles(
        assetTree,
        "*.json",
        SearchOption.AllDirectories
      )
    )
      yield return Path.GetRelativePath(root, file).Replace('\\', '/');
  }
}
