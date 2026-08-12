using System;
using System.Collections.Generic;
using ExpandedLib.Blocks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Declared block-entity state: a field is named once and both directions follow from that. The pair of
/// hand-written <c>ToTreeAttributes</c>/<c>FromTreeAttributes</c> this replaces spells every field
/// twice, and a field added to one side and forgotten on the other still compiles - it saves and never
/// loads, or loads and never reaches the client.
/// </summary>
public class ExBlockStateTests {
  private enum Mode {
    Idle = 0,
    Running = 7,
  }

  // Every supported kind at once, so a round trip covers the whole surface rather than one type.
  private sealed class Bag {
    public bool Flag;
    public int Count;
    public long Ticks;
    public float Temp;
    public double Precise;
    public string? Label;
    public Mode State;
    public BlockPos? Anchor;
    public ItemStack? Stack;

    public ExBlockState Declare() =>
      new ExBlockState()
        .Bool("flag", () => Flag, v => Flag = v)
        .Int("count", () => Count, v => Count = v)
        .Long("ticks", () => Ticks, v => Ticks = v)
        .Float("temp", () => Temp, v => Temp = v)
        .Double("precise", () => Precise, v => Precise = v)
        .String("label", () => Label, v => Label = v)
        .Enum("state", () => State, v => State = v)
        .Pos("anchor", () => Anchor, v => Anchor = v)
        .Stack("stack", () => Stack, v => Stack = v);
  }

  #region Round trip

  [Fact]
  public void Every_declared_kind_survives_a_round_trip() {
    var world = new TestWorld();
    Item item = world.RegisterItem("test:widget");

    var source = new Bag {
      Flag = true,
      Count = 42,
      Ticks = 9_000_000_000L,
      Temp = 1234.5f,
      Precise = 0.1234567890123,
      Label = "hot",
      State = Mode.Running,
      Anchor = new BlockPos(3, -4, 5),
      Stack = new ItemStack(item),
    };

    var tree = new TreeAttribute();
    source.Declare().ToTree(tree);

    var target = new Bag();
    target.Declare().FromTree(tree, world.World);

    Assert.True(target.Flag);
    Assert.Equal(42, target.Count);
    Assert.Equal(9_000_000_000L, target.Ticks);
    Assert.Equal(1234.5f, target.Temp);
    Assert.Equal(0.1234567890123, target.Precise, 12);
    Assert.Equal("hot", target.Label);
    Assert.Equal(Mode.Running, target.State);
    Assert.Equal(new BlockPos(3, -4, 5), target.Anchor);
    Assert.Equal(item.Code, target.Stack?.Collectible?.Code);
  }

  [Fact]
  public void Absent_reference_fields_read_back_as_null_not_as_a_default() {
    var world = new TestWorld();

    var tree = new TreeAttribute();
    new Bag().Declare().ToTree(tree); // everything null

    var target = new Bag {
      Label = "stale",
      Anchor = new BlockPos(1, 1, 1),
      Stack = new ItemStack(world.RegisterItem("test:stale")),
    };
    target.Declare().FromTree(tree, world.World);

    // A position must not come back as the origin, which is a real cell and would silently re-anchor
    // whatever reads it.
    Assert.Null(target.Label);
    Assert.Null(target.Anchor);
    Assert.Null(target.Stack);
  }

  [Fact]
  public void An_enum_persists_as_its_value_so_a_rename_does_not_move_it() {
    var bag = new Bag { State = Mode.Running };
    var tree = new TreeAttribute();
    bag.Declare().ToTree(tree);

    Assert.Equal(7, tree.GetInt("state"));
  }

  #endregion

  #region The declaration itself

  [Fact]
  public void Declaring_one_key_twice_is_refused() {
    // Two fields on one key is the copy-paste slip this type exists to prevent: the second silently
    // overwrites the first on save, and both load the same value.
    int a = 0,
      b = 0;

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
      () =>
        new ExBlockState()
          .Int("same", () => a, v => a = v)
          .Int("same", () => b, v => b = v)
    );

    Assert.Contains("same", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void The_declared_keys_are_reportable_in_declaration_order() {
    ExBlockState state = new Bag().Declare();

    Assert.Equal(
      [
        "flag",
        "count",
        "ticks",
        "temp",
        "precise",
        "label",
        "state",
        "anchor",
        "stack",
      ],
      state.Keys
    );
  }

  #endregion

  #region Collectible mappings (what makes a schematic paste survive)

  [Fact]
  public void A_declared_stack_records_its_collectible_for_a_schematic_save() {
    var world = new TestWorld();
    Item item = world.RegisterItem("test:widget");
    var bag = new Bag { Stack = new ItemStack(item) };

    var blocks = new Dictionary<int, AssetLocation>();
    var items = new Dictionary<int, AssetLocation>();
    bag.Declare().StoreCollectibleMappings(world.World, blocks, items);

    // ItemStack.ToBytes writes the runtime id, so without this the paste resolves whatever owns that
    // id in the destination world.
    Assert.Equal(item.Code, items[item.Id]);
  }

  [Fact]
  public void A_stack_the_destination_world_does_not_know_is_dropped_not_mis_resolved() {
    var world = new TestWorld();
    Item item = world.RegisterItem("test:widget");
    var bag = new Bag { Stack = new ItemStack(item) };

    // An id the destination cannot map. FixMapping leaves Id at the source world's value, which would
    // resolve to whatever owns that id there - so the stack must go rather than become another item.
    bag.Declare()
      .LoadCollectibleMappings(
        world.World,
        [],
        new Dictionary<int, AssetLocation> {
          [item.Id] = new AssetLocation("test:gone"),
        }
      );

    Assert.Null(bag.Stack);
  }

  #endregion
}
