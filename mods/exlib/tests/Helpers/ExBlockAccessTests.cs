using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The typed block-entity lookup that replaces <c>GetBlockEntity(pos) is X be</c> with a null guard,
/// and its one-step and multi-step neighbour walks.
/// </summary>
public class ExBlockAccessTests {
  private interface IMarker {
    int Value { get; }
  }

  private sealed class MarkerBlockEntity : BlockEntity, IMarker {
    public int Value { get; init; }
  }

  private sealed class OtherBlockEntity : BlockEntity { }

  private static readonly BlockPos Origin = new(10, 10, 10, 0);

  private static TestWorld PlaceMarker(BlockPos pos, int value = 1) {
    var world = new TestWorld();
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "test:marker", 1),
      new MarkerBlockEntity { Value = value }
    );
    return world;
  }

  [Fact]
  public void BlockEntity_finds_a_match_at_the_position() {
    TestWorld world = PlaceMarker(Origin, 5);

    MarkerBlockEntity? be = world.Accessor.BlockEntity<MarkerBlockEntity>(Origin);

    Assert.NotNull(be);
    Assert.Equal(5, be!.Value);
  }

  [Fact]
  public void BlockEntity_is_null_for_an_empty_cell() {
    var world = new TestWorld();

    Assert.Null(world.Accessor.BlockEntity<MarkerBlockEntity>(Origin));
  }

  [Fact]
  public void BlockEntity_is_null_for_the_wrong_type() {
    TestWorld world = PlaceMarker(Origin);

    Assert.Null(world.Accessor.BlockEntity<OtherBlockEntity>(Origin));
  }

  [Fact]
  public void BlockEntity_matches_through_an_interface() {
    TestWorld world = PlaceMarker(Origin, 7);

    IMarker? be = world.Accessor.BlockEntity<IMarker>(Origin);

    Assert.NotNull(be);
    Assert.Equal(7, be!.Value);
  }

  [Fact]
  public void BlockEntity_is_null_for_an_unloaded_chunk() {
    TestWorld world = PlaceMarker(Origin);
    world.UnloadChunkAt(Origin);

    Assert.Null(world.Accessor.BlockEntity<MarkerBlockEntity>(Origin));
  }

  [Fact]
  public void TryGetBlockEntity_reports_a_hit() {
    TestWorld world = PlaceMarker(Origin, 9);

    bool found = world.Accessor.TryGetBlockEntity(Origin, out MarkerBlockEntity? be);

    Assert.True(found);
    Assert.Equal(9, be!.Value);
  }

  [Fact]
  public void TryGetBlockEntity_reports_a_miss() {
    var world = new TestWorld();

    bool found = world.Accessor.TryGetBlockEntity(
      Origin,
      out MarkerBlockEntity? be
    );

    Assert.False(found);
    Assert.Null(be);
  }

  [Fact]
  public void Neighbour_finds_the_block_entity_one_step_over() {
    BlockPos north = Origin.AddCopy(BlockFacing.NORTH);
    TestWorld world = PlaceMarker(north, 3);

    MarkerBlockEntity? be = world.Accessor.Neighbour<MarkerBlockEntity>(
      Origin,
      BlockFacing.NORTH
    );

    Assert.NotNull(be);
    Assert.Equal(3, be!.Value);
  }

  [Fact]
  public void Neighbour_is_null_when_nothing_of_that_type_stands_there() {
    var world = new TestWorld();

    Assert.Null(
      world.Accessor.Neighbour<MarkerBlockEntity>(Origin, BlockFacing.NORTH)
    );
  }

  [Fact]
  public void Neighbours_yields_only_matches_over_all_faces() {
    var world = new TestWorld();
    world.Place(
      Origin.AddCopy(BlockFacing.NORTH),
      TestBlocks.Configure(new Block(), "test:marker-n", 1),
      new MarkerBlockEntity { Value = 1 }
    );
    // The other block entity type sits on the east face and must not be yielded.
    world.Place(
      Origin.AddCopy(BlockFacing.EAST),
      TestBlocks.Configure(new Block(), "test:other-e", 2),
      new OtherBlockEntity()
    );
    world.Place(
      Origin.AddCopy(BlockFacing.UP),
      TestBlocks.Configure(new Block(), "test:marker-up", 3),
      new MarkerBlockEntity { Value = 2 }
    );

    var found = world.Accessor.Neighbours<MarkerBlockEntity>(Origin).ToList();

    Assert.Equal(2, found.Count);
    Assert.Contains(found, f => f.Facing == BlockFacing.NORTH && f.Entity.Value == 1);
    Assert.Contains(found, f => f.Facing == BlockFacing.UP && f.Entity.Value == 2);
  }

  [Fact]
  public void Neighbours_honours_a_facing_subset() {
    var world = new TestWorld();
    world.Place(
      Origin.AddCopy(BlockFacing.NORTH),
      TestBlocks.Configure(new Block(), "test:marker-n", 1),
      new MarkerBlockEntity { Value = 1 }
    );
    world.Place(
      Origin.AddCopy(BlockFacing.UP),
      TestBlocks.Configure(new Block(), "test:marker-up", 2),
      new MarkerBlockEntity { Value = 2 }
    );

    var found = world
      .Accessor.Neighbours<MarkerBlockEntity>(Origin, BlockFacing.HORIZONTALS)
      .ToList();

    // The one on UP is outside the requested subset, so only NORTH comes back.
    Assert.Single(found);
    Assert.Equal(BlockFacing.NORTH, found[0].Facing);
  }
}
