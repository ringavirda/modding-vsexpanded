using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Per-cell partial-fill boxes, which let a mega-block footprint cell be a slab or any other shape
/// instead of a full cube: how <c>fillerOffsets</c> JSON parses into boxes, how they rotate into the
/// placed orientation, how they survive the filler BE's save tree, and how the filler block hands them
/// back for collision and selection, falling back to the full cube when a cell has none.
/// </summary>
public class StructureFillerBoxesTests {
  #region Parsing

  [Fact]
  public void Cell_without_boxes_parses_as_a_full_cube_null() {
    var offsets = ReadOffsets(
      "[{ \"x\": 1, \"y\": 0, \"z\": 2, \"allowAttach\": true }]"
    );

    var off = Assert.Single(offsets);
    Assert.Equal(new Vec3i(1, 0, 2), off.Offset);
    Assert.True(off.AllowAttach);
    Assert.Null(off.CollisionBoxes);
  }

  [Fact]
  public void A_single_collisionBox_parses_to_one_box() {
    var offsets = ReadOffsets(
      "[{ \"x\": 0, \"y\": 1, \"z\": 0, \"collisionBox\": "
        + "{ \"x1\": 0, \"y1\": 0, \"z1\": 0, \"x2\": 1, \"y2\": 0.5, \"z2\": 1 } }]"
    );

    var boxes = Assert.Single(offsets).CollisionBoxes;
    Assert.NotNull(boxes);
    AssertBox(new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f), Assert.Single(boxes));
  }

  [Fact]
  public void A_collisionBoxes_array_parses_to_multiple_boxes() {
    var offsets = ReadOffsets(
      "[{ \"x\": 0, \"y\": 0, \"z\": 0, \"collisionBoxes\": ["
        + "{ \"x1\": 0, \"y1\": 0, \"z1\": 0, \"x2\": 1, \"y2\": 0.5, \"z2\": 1 },"
        + "{ \"x1\": 0, \"y1\": 0.5, \"z1\": 0, \"x2\": 0.5, \"y2\": 1, \"z2\": 1 }"
        + "] }]"
    );

    var boxes = Assert.Single(offsets).CollisionBoxes;
    Assert.NotNull(boxes);
    Assert.Equal(2, boxes.Length);
    AssertBox(new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f), boxes[0]);
    AssertBox(new Cuboidf(0f, 0.5f, 0f, 0.5f, 1f, 1f), boxes[1]);
  }

  [Fact]
  public void An_empty_collisionBoxes_array_falls_back_to_null() {
    var offsets = ReadOffsets(
      "[{ \"x\": 0, \"y\": 0, \"z\": 0, \"collisionBoxes\": [] }]"
    );
    Assert.Null(Assert.Single(offsets).CollisionBoxes);
  }

  #endregion

  #region Rotation

  [Fact]
  public void Boxes_are_unchanged_at_angle_0() {
    var cell = Assert.Single(FootprintAt(0));
    AssertBox(
      new Cuboidf(0f, 0f, 0f, 0.5f, 1f, 1f),
      Assert.Single(cell.CollisionBoxes!)
    );
  }

  [Fact]
  public void Boxes_mirror_across_the_cell_centre_at_angle_180() {
    // A north box on the low-x half becomes the high-x half after a 180° turn; y is untouched.
    var cell = Assert.Single(FootprintAt(180));
    AssertBox(
      new Cuboidf(0.5f, 0f, 0f, 1f, 1f, 1f),
      Assert.Single(cell.CollisionBoxes!)
    );
  }

  [Theory]
  [InlineData(0)]
  [InlineData(90)]
  [InlineData(180)]
  [InlineData(270)]
  public void Rotation_preserves_box_count_and_vertical_extent(int angle) {
    // Rotation is horizontal only, so a slab keeps its height at every angle.
    var box = Assert.Single(Assert.Single(FootprintAt(angle)).CollisionBoxes!);
    Assert.Equal(0f, box.Y1, 4);
    Assert.Equal(1f, box.Y2, 4);
  }

  [Fact]
  public void A_cell_without_boxes_resolves_to_null_boxes() {
    var host = new Host(Offsets("[{ \"x\": 1, \"y\": 0, \"z\": 0 }]"));
    var cell = Assert.Single(
      StructureFillers.FootprintCells(host, new BlockPos(0, 0, 0), 90)
    );
    Assert.Null(cell.CollisionBoxes);
  }

  #endregion

  #region Serialization

  [Fact]
  public void CollisionBoxes_round_trip_through_the_save_tree() {
    var be = new BlockEntityStructureFiller {
      Principal = new BlockPos(3, 4, 5),
      AllowAttach = true,
      CollisionBoxes =
      [
        new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f),
        new Cuboidf(0f, 0.5f, 0f, 0.5f, 1f, 1f),
      ],
    };

    var restored = RoundTrip(be);

    Assert.NotNull(restored.CollisionBoxes);
    Assert.Equal(2, restored.CollisionBoxes.Length);
    AssertBox(
      new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f),
      restored.CollisionBoxes[0]
    );
    AssertBox(
      new Cuboidf(0f, 0.5f, 0f, 0.5f, 1f, 1f),
      restored.CollisionBoxes[1]
    );
  }

  [Fact]
  public void A_full_cube_cell_keeps_null_boxes_across_the_tree() {
    var be = new BlockEntityStructureFiller {
      Principal = new BlockPos(1, 2, 3),
    };
    Assert.Null(RoundTrip(be).CollisionBoxes);
  }

  #endregion

  #region Block boxes

  [Fact]
  public void The_filler_block_returns_the_cells_partial_boxes() {
    Cuboidf[] partial = [new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f)];
    var block = new BlockStructureFiller();
    var pos = new BlockPos(0, 0, 0);
    var ba = AccessorWith(
      pos,
      new BlockEntityStructureFiller { CollisionBoxes = partial }
    );

    Assert.Same(partial, block.GetCollisionBoxes(ba, pos));
    Assert.Same(partial, block.GetSelectionBoxes(ba, pos));
  }

  [Fact]
  public void The_filler_block_falls_back_to_its_own_boxes_when_a_cell_has_no_boxes() {
    // A cell with an empty BE and a cell with no BE must resolve identically: both fall through to the
    // block's own JSON-configured boxes rather than the partial path.
    var block = new BlockStructureFiller();
    var pos = new BlockPos(0, 0, 0);
    var emptyBe = AccessorWith(pos, new BlockEntityStructureFiller()); // null CollisionBoxes
    var noBe = Substitute.For<IBlockAccessor>(); // GetBlockEntity -> null

    Assert.Equal(
      block.GetCollisionBoxes(noBe, pos),
      block.GetCollisionBoxes(emptyBe, pos)
    );
    Assert.Equal(
      block.GetSelectionBoxes(noBe, pos),
      block.GetSelectionBoxes(emptyBe, pos)
    );
  }

  #endregion

  #region Helpers

  private static JsonObject Offsets(string json) => new(JArray.Parse(json));

  private static System.Collections.Generic.List<FillerOffset> ReadOffsets(
    string json
  ) => StructureFillers.ReadOffsets(Offsets(json));

  /// <summary>
  /// Resolves a single low-x-half slab cell at the given angle, so rotation can be asserted in
  /// isolation. The north box is <c>(0,0,0 - 0.5,1,1)</c>.
  /// </summary>
  private static System.Collections.Generic.List<FillerCell> FootprintAt(
    int angle
  ) {
    var host = new Host(
      Offsets(
        "[{ \"x\": 1, \"y\": 0, \"z\": 0, \"collisionBox\": "
          + "{ \"x1\": 0, \"y1\": 0, \"z1\": 0, \"x2\": 0.5, \"y2\": 1, \"z2\": 1 } }]"
      )
    );
    return StructureFillers.FootprintCells(host, new BlockPos(0, 0, 0), angle);
  }

  private static BlockEntityStructureFiller RoundTrip(
    BlockEntityStructureFiller be
  ) {
    // base.ToTreeAttributes writes Pos/Block, so the BE has to be sited like a placed one.
    var world = new TestWorld();
    var block = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      70
    );
    var pos = new BlockPos(1, 2, 3);
    be.Pos = pos;
    be.Block = block;

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    var restored = new BlockEntityStructureFiller { Pos = pos, Block = block };
    restored.FromTreeAttributes(tree, world.World);
    return restored;
  }

  private static IBlockAccessor AccessorWith(
    BlockPos pos,
    BlockEntityStructureFiller be
  ) {
    var ba = Substitute.For<IBlockAccessor>();
    ba.GetBlockEntity(pos).Returns(be);
    return ba;
  }

  private static void AssertBox(
    Cuboidf expected,
    Cuboidf actual,
    int precision = 4
  ) {
    Assert.Equal(expected.X1, actual.X1, precision);
    Assert.Equal(expected.Y1, actual.Y1, precision);
    Assert.Equal(expected.Z1, actual.Z1, precision);
    Assert.Equal(expected.X2, actual.X2, precision);
    Assert.Equal(expected.Y2, actual.Y2, precision);
    Assert.Equal(expected.Z2, actual.Z2, precision);
  }

  private sealed class Host(JsonObject? offsets) : IFillerHost {
    public JsonObject? FillerOffsets { get; } = offsets;
  }

  #endregion
}
