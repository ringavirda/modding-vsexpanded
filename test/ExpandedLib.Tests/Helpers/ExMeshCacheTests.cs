using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shared mesh cache's keying and lifetime. Tesselation itself needs a real client, so what is
/// checked here is the part that decides whether the cache is correct rather than merely fast: that a
/// key is built once per distinct input, and that two blocktypes cannot collide on one.
/// </summary>
public class ExMeshCacheTests {
  /// <summary>A client API whose ObjectCache is a real dictionary, which is all
  /// <c>ObjectCacheUtil</c> touches.</summary>
  private static ICoreClientAPI Capi() {
    var capi = Substitute.For<ICoreClientAPI>();
    capi.ObjectCache.Returns(new Dictionary<string, object>());
    return capi;
  }

  private static Block BlockNamed(string code) =>
    TestBlocks.Configure(new Block(), code, 1);

  [Fact]
  public void A_mesh_is_built_once_per_key_and_reused_after() {
    ICoreClientAPI capi = Capi();
    var mesh = new MeshData();
    int builds = 0;

    for (int i = 0; i < 3; i++) {
      MeshData? got = ExMeshCache.GetOrCreate(
        capi,
        "firebox|2|coke",
        () => {
          builds++;
          return mesh;
        }
      );
      Assert.Same(mesh, got);
    }

    // Without the cache this is the shape re-read, deep-cloned and re-tesselated once per call, on the
    // tesselation thread.
    Assert.Equal(1, builds);
  }

  [Fact]
  public void A_different_variant_key_is_a_different_mesh() {
    ICoreClientAPI capi = Capi();
    var first = new MeshData();
    var second = new MeshData();

    Assert.Same(first, ExMeshCache.GetOrCreate(capi, "a", () => first));
    Assert.Same(second, ExMeshCache.GetOrCreate(capi, "b", () => second));
  }

  [Fact]
  public void Two_blocktypes_sharing_a_variant_key_do_not_share_a_mesh() {
    // The failure this prevents renders one block as another, which reads as an art bug rather than a
    // caching one - so the block code is folded in for the caller instead of being their job.
    ICoreClientAPI capi = Capi();
    var firebox = new MeshData();
    var hearth = new MeshData();

    Assert.Same(
      firebox,
      ExMeshCache.GetOrCreate(
        capi,
        BlockNamed("iwex:firebox"),
        "2",
        () => firebox
      )
    );
    Assert.Same(
      hearth,
      ExMeshCache.GetOrCreate(
        capi,
        BlockNamed("iwex:hearth"),
        "2",
        () => hearth
      )
    );
  }

  [Fact]
  public void Invalidating_a_key_makes_the_next_request_rebuild() {
    ICoreClientAPI capi = Capi();
    int builds = 0;

    ExMeshCache.GetOrCreate(
      capi,
      "k",
      () => {
        builds++;
        return new MeshData();
      }
    );
    ExMeshCache.Invalidate(capi, "k");
    ExMeshCache.GetOrCreate(
      capi,
      "k",
      () => {
        builds++;
        return new MeshData();
      }
    );

    Assert.Equal(2, builds);
  }

  [Fact]
  public void A_blocks_shape_path_is_resolved_without_mutating_the_shared_shape() {
    // WithPathPrefixOnce mutates in place, and Block.Shape is shared by every instance of that blocktype:
    // resolving without the clone corrupts the path for every other reader, cumulatively.
    Block block = BlockNamed("iwex:firebox");
    block.Shape = new CompositeShape {
      Base = new AssetLocation("iwex:block/firebox"),
    };
    AssetLocation before = block.Shape.Base.Clone();

    AssetLocation resolved = ExMeshCache.ShapePathOf(block);

    Assert.Equal("shapes/block/firebox.json", resolved.Path);
    Assert.Equal(before.Path, block.Shape.Base.Path);
  }
}
