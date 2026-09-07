using System;
using System.Collections;
using System.Reflection;
using ExpandedLib.Blocks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The declarative rung: a field marked <see cref="PersistAttribute"/> needs no
/// <c>DeclareState</c> entry at all. <see cref="PersistScan"/> finds it by reflection once per
/// concrete type and compiles the accessor it then reuses for every instance.
/// </summary>
public class PersistAttributeTests {
  private enum Mode {
    Idle = 0,
    Running = 7,
  }

  /// <summary>The nested-tree case: a value that saves itself, mutated in place on load.</summary>
  private sealed class Wrapper : IPersistable {
    public int Value;

    public void ToTree(ITreeAttribute tree) => tree.SetInt("value", Value);

    public void FromTree(ITreeAttribute tree, IWorldAccessor world) =>
      Value = tree.GetInt("value");
  }

  // Every supported kind at once, named with a leading underscore so the default-key rule is
  // exercised alongside the round trip.
#pragma warning disable CS0169 // fixture fields are set/read only via [Persist] reflection, never by name
  private sealed class Bag : ExBlockEntity {
    [Persist]
    private bool _flag;

    [Persist]
    private int _count;

    [Persist]
    private long _ticks;

    [Persist]
    private float _temp;

    [Persist]
    private double _precise;

    [Persist]
    private string? _label;

    [Persist]
    private Mode _mode;

    [Persist]
    private BlockPos? _anchor;

    [Persist]
    private ItemStack? _stack;

    [Persist]
    private readonly Wrapper _wrapper = new();

    protected override void DeclareState(ExBlockState state) { }
  }

  private sealed class ExplicitKeyEntity : ExBlockEntity {
    [Persist("customKey")]
    private int _value;

    protected override void DeclareState(ExBlockState state) { }
  }

  private sealed class RenamedEntity : ExBlockEntity {
    [Persist(Legacy = "oldName")]
    private int _value;

    protected override void DeclareState(ExBlockState state) { }
  }

  private class BaseEntity : ExBlockEntity {
    [Persist]
    private int _baseValue;

    protected override void DeclareState(ExBlockState state) { }
  }

  private sealed class DerivedEntity : BaseEntity {
    [Persist]
    private int _derivedValue;
  }

  private sealed class BadEntity : ExBlockEntity {
    [Persist]
    private object? _thing;

    protected override void DeclareState(ExBlockState state) { }
  }

  private sealed class MixedEntity : ExBlockEntity {
    [Persist]
    private int _attributed;

    public float Explicit;

    protected override void DeclareState(ExBlockState state) =>
      state.Float("explicit", () => Explicit, v => Explicit = v);
  }

  private sealed class ScanEntity : ExBlockEntity {
    [Persist]
    private int _value;

    protected override void DeclareState(ExBlockState state) { }
  }
#pragma warning restore CS0169

  // ToTreeAttributes reads Block.IsMissing before it ever reaches Persisted, so every instance under test
  // needs a real block even though nothing here cares which one.
  private static void Place(BlockEntity be) {
    be.Pos = new BlockPos(0, 0, 0);
    be.Block = TestBlocks.Configure(new Block(), "test:persist", 1);
  }

  #region Round trip

  [Fact]
  public void Every_supported_kind_round_trips() {
    var world = new TestWorld();
    Item item = world.RegisterItem("test:widget");

    var source = new Bag();
    Place(source);
    ReflectionHelpers.SetField(source, "_flag", true);
    ReflectionHelpers.SetField(source, "_count", 42);
    ReflectionHelpers.SetField(source, "_ticks", 9_000_000_000L);
    ReflectionHelpers.SetField(source, "_temp", 1234.5f);
    ReflectionHelpers.SetField(source, "_precise", 0.1234567890123);
    ReflectionHelpers.SetField(source, "_label", "hot");
    ReflectionHelpers.SetField(source, "_mode", Mode.Running);
    ReflectionHelpers.SetField(source, "_anchor", new BlockPos(3, -4, 5));
    ReflectionHelpers.SetField(source, "_stack", new ItemStack(item));
    ((Wrapper)ReflectionHelpers.GetField(source, "_wrapper")!).Value = 77;

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new Bag();
    target.FromTreeAttributes(tree, world.World);

    Assert.True((bool)ReflectionHelpers.GetField(target, "_flag")!);
    Assert.Equal(42, (int)ReflectionHelpers.GetField(target, "_count")!);
    Assert.Equal(
      9_000_000_000L,
      (long)ReflectionHelpers.GetField(target, "_ticks")!
    );
    Assert.Equal(1234.5f, (float)ReflectionHelpers.GetField(target, "_temp")!);
    Assert.Equal(
      0.1234567890123,
      (double)ReflectionHelpers.GetField(target, "_precise")!,
      12
    );
    Assert.Equal("hot", (string?)ReflectionHelpers.GetField(target, "_label"));
    Assert.Equal(
      Mode.Running,
      (Mode)ReflectionHelpers.GetField(target, "_mode")!
    );
    Assert.Equal(
      new BlockPos(3, -4, 5),
      (BlockPos?)ReflectionHelpers.GetField(target, "_anchor")
    );
    Assert.Equal(
      item.Code,
      ((ItemStack)ReflectionHelpers.GetField(target, "_stack")!)
        .Collectible
        .Code
    );
    Assert.Equal(
      77,
      ((Wrapper)ReflectionHelpers.GetField(target, "_wrapper")!).Value
    );
  }

  [Fact]
  public void An_enum_is_stored_as_its_underlying_int() {
    var source = new Bag();
    Place(source);
    ReflectionHelpers.SetField(source, "_mode", Mode.Running);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Assert.Equal(7, tree.GetInt("mode"));
  }

  #endregion

  #region Keys

  [Fact]
  public void The_default_key_strips_a_leading_underscore() {
    var source = new Bag();
    Place(source);
    ReflectionHelpers.SetField(source, "_count", 9);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Assert.Equal(9, tree.GetInt("count"));
  }

  [Fact]
  public void An_explicit_key_overrides_the_default_name() {
    var source = new ExplicitKeyEntity();
    Place(source);
    ReflectionHelpers.SetField(source, "_value", 5);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Assert.Equal(5, tree.GetInt("customKey"));
    Assert.False(tree.HasAttribute("value"));
  }

  #endregion

  #region Legacy

  [Fact]
  public void Legacy_is_read_only_when_the_new_key_is_absent() {
    var tree = new TreeAttribute();
    tree.SetInt("oldName", 11);

    var target = new RenamedEntity();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(11, (int)ReflectionHelpers.GetField(target, "_value")!);
  }

  [Fact]
  public void Legacy_is_ignored_once_the_new_key_is_present() {
    var tree = new TreeAttribute();
    tree.SetInt("oldName", 11);
    tree.SetInt("value", 99);

    var target = new RenamedEntity();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(99, (int)ReflectionHelpers.GetField(target, "_value")!);
  }

  [Fact]
  public void A_save_writes_only_the_new_key_never_the_legacy_one() {
    var source = new RenamedEntity();
    Place(source);
    ReflectionHelpers.SetField(source, "_value", 3);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Assert.Equal(3, tree.GetInt("value"));
    Assert.False(tree.HasAttribute("oldName"));
  }

  #endregion

  #region Inheritance and combination

  [Fact]
  public void A_subclass_inherits_its_bases_persist_members() {
    var source = new DerivedEntity();
    Place(source);
    ReflectionHelpers.SetField(source, "_baseValue", 1);
    ReflectionHelpers.SetField(source, "_derivedValue", 2);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new DerivedEntity();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(1, (int)ReflectionHelpers.GetField(target, "_baseValue")!);
    Assert.Equal(2, (int)ReflectionHelpers.GetField(target, "_derivedValue")!);
  }

  [Fact]
  public void Persist_and_DeclareState_combine() {
    var source = new MixedEntity { Explicit = 4.5f };
    Place(source);
    ReflectionHelpers.SetField(source, "_attributed", 3);

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    Assert.Equal(3, tree.GetInt("attributed"));
    Assert.Equal(4.5f, tree.GetFloat("explicit"));

    var target = new MixedEntity();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(3, (int)ReflectionHelpers.GetField(target, "_attributed")!);
    Assert.Equal(4.5f, target.Explicit);
  }

  #endregion

  #region Failure and caching

  [Fact]
  public void An_unsupported_member_type_throws_naming_the_member() {
    var source = new BadEntity();
    Place(source);

    NotSupportedException ex = Assert.Throws<NotSupportedException>(() =>
      source.ToTreeAttributes(new TreeAttribute())
    );

    Assert.Contains("_thing", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void The_scan_is_cached_per_type_not_rebuilt_per_instance() {
    // PersistScan caches the compiled binder array by CLR type: reading through the same private
    // dictionary after two separate instances declare their state must find the identical array,
    // not two equal-but-distinct rebuilds.
    FieldInfo cacheField =
      typeof(PersistScan).GetField(
        "_cache",
        BindingFlags.NonPublic | BindingFlags.Static
      ) ?? throw new InvalidOperationException("PersistScan._cache not found.");
    var cache = (IDictionary)cacheField.GetValue(null)!;

    var a = new ScanEntity();
    ReflectionHelpers.GetProperty(a, "Persisted");
    object? firstBinders = cache[typeof(ScanEntity)];

    var b = new ScanEntity();
    ReflectionHelpers.GetProperty(b, "Persisted");
    object? secondBinders = cache[typeof(ScanEntity)];

    Assert.NotNull(firstBinders);
    Assert.Same(firstBinders, secondBinders);
  }

  #endregion
}
