using System.Collections.Generic;

namespace ExpandedLib.Testing;

/// <summary>
/// The highest version published per modid, hand-edited when a release goes out. Cannot be derived
/// from <c>dist/Releases/</c>: that folder is gitignored build output the packager clears on every
/// run, so it holds only the newest local build, not release history. <c>ModinfoTests</c> (exlib)
/// holds every source version strictly above the row here for the same modid, so a tree stamped at
/// or below an already-published version fails loudly instead of shipping an update that never
/// supersedes the version it is meant to replace.
/// </summary>
public static class ReleasedVersions {
  /// <summary>modid -> the highest version ever published under it, across every branch.</summary>
  public static readonly IReadOnlyDictionary<string, string> HighestPublished =
    new Dictionary<string, string> {
      ["exlib"] = "0.7.2",
      ["ppex"] = "0.6.8",
      ["smex"] = "0.9.8",
    };
}
