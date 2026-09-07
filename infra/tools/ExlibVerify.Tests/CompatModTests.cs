using System.IO;
using Xunit;

namespace ExpandedLib.Verify.Tests;

/// <summary>
/// Runs the checker over two real, unmodified third-party mods vendored for compatibility research
/// (<c>.compat/_im</c>, <c>.compat/industrialstory</c> - see <c>docs/internal/research</c>). Neither
/// is expected to come back clean: both are hybrid code+JSON mods that register some content in C#,
/// invisible to a JSON-only scan, so a real finding here is not necessarily a defect in either mod -
/// see the tool's own report for what it actually found. What these facts guard is only that the
/// tool never crashes on a real mod's shape, however unusual (unknown recipe kinds, compat patches
/// against mods this run has not loaded, and so on).
/// </summary>
public class CompatModTests {
  [Fact]
  public void Improved_metallurgy_does_not_crash_the_checker() {
    string? repoRoot = FixturePath.RepoRoot();
    if (repoRoot == null)
      return; // Running outside the checkout - .compat/ does not exist to check.

    string modPath = Path.Combine(repoRoot, ".compat", "_im");
    if (!Directory.Exists(modPath))
      return;

    int exit = Runner.Run(
      [modPath],
      TextWriter.Null,
      TextWriter.Null,
      out var findings
    );
    Assert.NotEqual(2, exit); // 2 means the tool itself failed to load the mod or the game.
    Assert.NotEmpty(findings); // A hybrid mod this size always has at least one finding to report.
  }

  [Fact]
  public void Industrialstory_does_not_crash_the_checker() {
    string? repoRoot = FixturePath.RepoRoot();
    if (repoRoot == null)
      return;

    string modPath = Path.Combine(repoRoot, ".compat", "industrialstory");
    if (!Directory.Exists(modPath))
      return;

    int exit = Runner.Run(
      [modPath],
      TextWriter.Null,
      TextWriter.Null,
      out var findings
    );
    Assert.NotEqual(2, exit);
    Assert.NotEmpty(findings);
  }
}
