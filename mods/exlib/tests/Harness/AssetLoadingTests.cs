// HelloExpanded only builds for the current game version, so a real-asset load of it can only run
// there; the API under test (TestWorld.LoadAssets) itself compiles and runs on every game version.
#if GAME_GE_1_22
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

    // BlockHello's def carries hellomodule's BlockBehaviorGreeter; LoadAssets loads only the one mod
    // named in its own path, so hellomodule's compiled dll must already be loaded for
    // BlockHello.Definitions to resolve it - the same as the real game loader, which loads every
    // installed mod's assembly into the one process before any of them runs.
    string moduleBinPath = Path.Combine(RepoPaths.Root, "samples", "HelloModule", "bin");
    Assembly.LoadFrom(
      Directory
        .EnumerateFiles(moduleBinPath, "hellomodule.dll", SearchOption.AllDirectories)
        .First()
    );

    using var world = new TestWorld();

    world.LoadAssets(samplePath);

    Block? resolved = null;
    foreach (string side in new[] { "n", "e", "s", "w" }) {
      Block? block = world.World.GetBlock(new AssetLocation($"helloexpanded:hello-{side}"));
      Assert.NotNull(block);
      Assert.Equal("HelloExpanded.BlockHello", block.GetType().FullName);
      Assert.Equal(side, block.Variant["side"]);
      resolved = block;
    }

    // Vintagestory.ServerMods.NoObf.BlockType.InitBlock, which reads the JSON "behaviors" array and
    // resolves each name through the class registry, does not carry that array through to the
    // per-variant Block this isolated harness resolves - block.BlockBehaviors comes back empty
    // regardless of whether the class is registered, a gap in the harness rather than in BlockHello's
    // def. This reads BlockHello's own def instead, the source both the harness and a real server
    // resolve the block from, and checks it declares the cross-assembly behaviour code.
    Type blockHello = resolved!.GetType();
    var defs = (System.Collections.IEnumerable)
      blockHello
        .GetMethod("Definitions", BindingFlags.Public | BindingFlags.Static)!
        .Invoke(null, ["helloexpanded"])!;
    object def = Assert.Single(defs.Cast<object>());
    string json = def.GetType().GetMethod("ToJson")!.Invoke(def, null)!.ToString()!;
    Assert.Contains("hellomodule.BlockBehaviorGreeter", json);
  }
}
#endif
