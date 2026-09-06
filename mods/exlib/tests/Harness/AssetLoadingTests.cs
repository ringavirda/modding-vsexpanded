// HelloExpanded only builds for the current game version, so a real-asset load of it can only run
// there; the API under test (TestWorld.LoadAssets) itself compiles and runs on every game version.
#if GAME_GE_1_22
using System.IO;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="TestWorld.LoadAssets"/> against the getting-started sample: a real block, resolved by
/// the engine's own object loader from HelloExpanded's code-first definition, with its variants
/// intact - not a <see cref="TestWorld.RegisterItem"/> stand-in.
/// </summary>
public class AssetLoadingTests {
  [Fact]
  public void Hello_block_resolves_with_its_orientation_variants() {
    string samplePath = Path.Combine(RepoPaths.Root, "samples", "HelloExpanded");
    using var world = new TestWorld();

    world.LoadAssets(samplePath);

    foreach (string side in new[] { "n", "e", "s", "w" }) {
      Block? block = world.World.GetBlock(new AssetLocation($"helloexpanded:hello-{side}"));
      Assert.NotNull(block);
      Assert.Equal("HelloExpanded.BlockHello", block.GetType().FullName);
      Assert.Equal(side, block.Variant["side"]);
    }
  }
}
#endif
