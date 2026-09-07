using System;
using System.IO;

namespace ExpandedLib.Verify.Tests;

/// <summary>Resolves a fixture folder under <c>Fixtures/</c> straight off disk - the fixtures are
/// never copied into the test output (see the csproj's <c>CopyToOutputDirectory="Never"</c>),
/// since <see cref="ModSource"/> reads a mod from wherever it actually lives.</summary>
internal static class FixturePath {
  /// <summary>The absolute path to <c>Fixtures/&lt;name&gt;</c>, found by walking up from the test
  /// binary's directory to the one holding <c>ExlibVerify.Tests.csproj</c>.</summary>
  public static string Of(string name) {
    for (
      DirectoryInfo? dir = new(AppContext.BaseDirectory);
      dir != null;
      dir = dir.Parent
    ) {
      string csproj = Path.Combine(dir.FullName, "ExlibVerify.Tests.csproj");
      if (File.Exists(csproj))
        return Path.Combine(dir.FullName, "Fixtures", name);
    }
    throw new DirectoryNotFoundException(
      "Could not find ExlibVerify.Tests.csproj above "
        + AppContext.BaseDirectory
    );
  }

  /// <summary>The repository root, found by walking up from the test binary's directory to the one
  /// holding <c>VintageStory.sln</c>. Null when this assembly runs outside the checkout (a packaged
  /// nupkg's own test run, say) - callers skip a repo-relative fixture in that case rather than
  /// throwing.</summary>
  public static string? RepoRoot() {
    for (
      DirectoryInfo? dir = new(AppContext.BaseDirectory);
      dir != null;
      dir = dir.Parent
    )
      if (File.Exists(Path.Combine(dir.FullName, "VintageStory.sln")))
        return dir.FullName;
    return null;
  }
}
