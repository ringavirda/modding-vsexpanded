using System.Linq;
using ExpandedLib.Checks;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="AssetCheckSource.Domains"/> against a <see cref="TestModLoader"/>: only exlib and its
/// declared dependents are in scope, never a mod with no exlib dependency at all.
/// </summary>
public class AssetCheckSourceTests {
  [Fact]
  public void Domains_is_exlib_plus_its_dependents_only() {
    using var world = new TestWorld();
    world.Mods.Add("dependent", "1.0.0", dependencies: "exlib");
    world.Mods.Add("bystander", "1.0.0");

    var source = new AssetCheckSource(world.Api);

    Assert.Equal(["dependent", "exlib"], source.Domains.OrderBy(d => d));
  }
}
