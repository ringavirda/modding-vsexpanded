using System.IO;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="RepoPaths"/>'s domain lookup: a known domain resolves to its declared mod, an unknown
/// one falls back to a folder of its own name rather than throwing, and <see cref="RepoPaths.Register"/>
/// lets a new mod declare its domain instead of hand-editing the harness.
/// </summary>
public class RepoPathsTests {
  [Fact]
  public void Assets_of_an_unknown_domain_falls_back_to_a_same_named_mod_folder() {
    string path = RepoPaths.Assets("nosuchdomain");
    Assert.Equal(
      Path.Combine(RepoPaths.Root, "mods", "nosuchdomain", "assets", "nosuchdomain"),
      path
    );
  }

  [Fact]
  public void Register_makes_a_new_domain_resolve_to_its_declared_mod() {
    RepoPaths.Register("harnesstest-domain", "harnesstest-mod");
    string path = RepoPaths.Assets("harnesstest-domain");
    Assert.Equal(
      Path.Combine(RepoPaths.Root, "mods", "harnesstest-mod", "assets", "harnesstest-domain"),
      path
    );
  }

  [Fact]
  public void Register_replaces_an_earlier_registration_for_the_same_domain() {
    RepoPaths.Register("harnesstest-replace", "harnesstest-first");
    RepoPaths.Register("harnesstest-replace", "harnesstest-second");
    string path = RepoPaths.Assets("harnesstest-replace");
    Assert.Equal(
      Path.Combine(RepoPaths.Root, "mods", "harnesstest-second", "assets", "harnesstest-replace"),
      path
    );
  }
}
