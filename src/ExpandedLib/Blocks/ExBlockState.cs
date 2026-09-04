using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks;

/// <summary>
/// A block entity's persisted fields, declared once and read and written from that one declaration.
/// Hand-writing <c>ToTreeAttributes</c> and <c>FromTreeAttributes</c> as a pair means every field is
/// spelled twice and a field added to one side and forgotten on the other still compiles: it saves and
/// never loads, or loads and never reaches the client.
/// <para>
/// Composable on purpose. <see cref="ExBlockEntity"/> owns one and needs no wiring, but a block entity
/// whose base is already spent - a container, a multiblock - can own one too and call
/// <see cref="ToTree"/> and <see cref="FromTree"/> from its own overrides.
/// </para>
/// </summary>
public sealed class ExBlockState {
  private sealed record Entry(
    string Key,
    Action<ITreeAttribute> Write,
    Action<ITreeAttribute, IWorldAccessor> Read,
    Func<ItemStack?>? GetStack,
    Action<ItemStack?>? SetStack
  );

  private readonly List<Entry> _entries = [];
  private readonly HashSet<string> _keys = [];

  /// <summary>The keys declared so far, in declaration order. Lets a guard assert what a block entity
  /// persists without reflecting over its fields.</summary>
  public IReadOnlyList<string> Keys => [.. _keys];

  private ExBlockState Add(
    string key,
    Action<ITreeAttribute> write,
    Action<ITreeAttribute, IWorldAccessor> read,
    Func<ItemStack?>? getStack = null,
    Action<ItemStack?>? setStack = null
  ) {
    // Two fields on one key is the copy-paste slip this whole type exists to make impossible: the
    // second silently overwrites the first on save and both load the same value. Caught at
    // declaration, which runs in the constructor, so it surfaces the first time one is built.
    if (!_keys.Add(key))
      throw new InvalidOperationException(
        $"Block-entity state key \"{key}\" is declared twice. Each field needs its own key."
      );
    _entries.Add(new Entry(key, write, read, getStack, setStack));
    return this;
  }

  #region Declaring fields

  public ExBlockState Bool(string key, Func<bool> get, Action<bool> set) =>
    Add(key, t => t.SetBool(key, get()), (t, _) => set(t.GetBool(key)));

  public ExBlockState Int(string key, Func<int> get, Action<int> set) =>
    Add(key, t => t.SetInt(key, get()), (t, _) => set(t.GetInt(key)));

  public ExBlockState Long(string key, Func<long> get, Action<long> set) =>
    Add(key, t => t.SetLong(key, get()), (t, _) => set(t.GetLong(key)));

  public ExBlockState Float(string key, Func<float> get, Action<float> set) =>
    Add(key, t => t.SetFloat(key, get()), (t, _) => set(t.GetFloat(key)));

  public ExBlockState Double(
    string key,
    Func<double> get,
    Action<double> set
  ) => Add(key, t => t.SetDouble(key, get()), (t, _) => set(t.GetDouble(key)));

  /// <summary>A string field. A null value is written as absent and reads back as null.</summary>
  public ExBlockState String(
    string key,
    Func<string?> get,
    Action<string?> set
  ) =>
    Add(
      key,
      t => {
        if (get() is { } v)
          t.SetString(key, v);
      },
      (t, _) => set(t.GetString(key, null))
    );

  /// <summary>An enum field, persisted as its underlying int so a renamed member keeps its value.</summary>
  public ExBlockState Enum<T>(string key, Func<T> get, Action<T> set)
    where T : struct, Enum =>
    Add(
      key,
      t => t.SetInt(key, Convert.ToInt32(get())),
      (t, _) => set((T)System.Enum.ToObject(typeof(T), t.GetInt(key)))
    );

  /// <summary>A position, written as three ints. Null round-trips as null rather than as origin.</summary>
  public ExBlockState Pos(
    string key,
    Func<BlockPos?> get,
    Action<BlockPos?> set
  ) =>
    Add(
      key,
      t => {
        if (get() is not { } p)
          return;
        t.SetInt($"{key}X", p.X);
        t.SetInt($"{key}Y", p.Y);
        t.SetInt($"{key}Z", p.Z);
      },
      (t, _) =>
        set(
          t[$"{key}X"] == null
            ? null
            : new BlockPos(
              t.GetInt($"{key}X"),
              t.GetInt($"{key}Y"),
              t.GetInt($"{key}Z")
            )
        )
    );

  /// <summary>
  /// An item stack. Beyond the round trip this carries the collectible id mapping both ways, which is
  /// what makes the block entity survive being pasted into another world - <c>ItemStack.ToBytes</c>
  /// writes the runtime id, so an unmapped stack resolves to whatever owns that id in the destination.
  /// </summary>
  public ExBlockState Stack(
    string key,
    Func<ItemStack?> get,
    Action<ItemStack?> set
  ) =>
    Add(
      key,
      t => {
        if (get() is { } v)
          t.SetItemstack(key, v);
      },
      (t, world) => {
        ItemStack? s = t.GetItemstack(key);
        // A stack read from a tree carries no resolved Collectible until it meets a world.
        s?.ResolveBlockOrItem(world);
        set(s);
      },
      get,
      set
    );

  #endregion

  #region Applying

  /// <summary>Writes every declared field into <paramref name="tree"/>.</summary>
  public void ToTree(ITreeAttribute tree) {
    foreach (Entry e in _entries)
      e.Write(tree);
  }

  /// <summary>Reads every declared field back out of <paramref name="tree"/>.</summary>
  public void FromTree(ITreeAttribute tree, IWorldAccessor world) {
    foreach (Entry e in _entries)
      e.Read(tree, world);
  }

  /// <summary>Records the code of every declared stack's collectible, for a schematic save.</summary>
  public void StoreCollectibleMappings(
    IWorldAccessor world,
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    foreach (Entry e in _entries) {
      if (e.GetStack?.Invoke() is not { } stack)
        continue;
      stack.Collectible?.OnStoreCollectibleMappings(
        world,
        new DummySlot(stack),
        blockIdMapping,
        itemIdMapping
      );
    }
  }

  /// <summary>
  /// Re-resolves every declared stack against the destination world. A stack whose collectible does not
  /// exist there is dropped rather than kept: <c>FixMapping</c> returning false leaves the id at the
  /// source world's value, which would resolve to whatever happens to own that id. Matches vanilla's
  /// <c>BlockEntityIngotMold</c>.
  /// </summary>
  public void LoadCollectibleMappings(
    IWorldAccessor world,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping
  ) {
    foreach (Entry e in _entries) {
      if (e.GetStack?.Invoke() is not { } stack)
        continue;
      if (!stack.FixMapping(oldBlockIdMapping, oldItemIdMapping, world))
        e.SetStack?.Invoke(null);
    }
  }

  #endregion
}
