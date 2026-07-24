using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// Generic, server-side world migrator for renamed/re-variantted blocks and items - and a purger for
/// ones a mod wants gone. Collects every <see cref="IBlockCodeMigration"/>,
/// <see cref="IItemCodeMigration"/> and <see cref="IBlockRemoval"/> in all loaded assemblies into
/// legacy-code → action tables and applies them to matching blocks as chunk columns load (items, which
/// are never placed, are rewritten wherever they are held as stacks). It matches on <see cref="Block.Code"/> (not a precomputed id, since the engine
/// renumbers ids on load) so it also catches the missing-block placeholders the engine keeps for
/// removed codes.
/// <para>
/// A plain migration is a bare block-id swap (state reconstructed from the new variant code); one
/// that also implements <see cref="IBlockEntityMigration"/> gets the old BE's tree handed to it. A
/// removal deletes the block in place. Either way the same matching content held as item stacks
/// (container BEs, player inventories) is rewritten or stripped too, migrations preserving stack size
/// and attributes.
/// </para>
/// <para>
/// The chunk-column walk itself (the RunGame sweep, the <c>ChunkColumnLoaded</c> subscription and the
/// <c>((y * cs) + z) * cs + x</c> decode) lives in <see cref="ChunkColumnSweeperModSystem"/>, shared
/// with the orphaned-BE healer.
/// </para>
/// </summary>
public class BlockMigrationModSystem : ChunkColumnSweeperModSystem
{
  /// <summary>One resolved action for a given legacy block code. A null
  /// <see cref="NewBlock"/> means "remove" (delete the block / drop the item stack); otherwise it is
  /// the replacement to swap in.</summary>
  internal readonly record struct RemapEntry(
    Block? NewBlock,
    AssetLocation OldCode,
    AssetLocation? NewCode,
    IBlockEntityMigration? BlockEntityMigration
  );

  /// <summary>One resolved item-stack rewrite (<see cref="IItemCodeMigration"/>): the replacement
  /// item to swap in for a legacy item code. Items are never in the world voxel grid, so this only
  /// applies to held stacks.</summary>
  internal readonly record struct ItemRemapEntry(
    Item NewItem,
    AssetLocation OldCode,
    AssetLocation NewCode
  );

  // Legacy block code -> replacement, merged across all discovered migrations. Keyed by code
  // (not id) because the engine can renumber block ids on load.
  internal readonly Dictionary<AssetLocation, RemapEntry> _remap = [];

  // Legacy item code -> replacement item, for stacks held in inventories/containers (items are never
  // placed in the world). Kept separate from _remap so a code that is both a block and an item (e.g.
  // slag) maps each independently; RemapInventory picks the table by the stack's class.
  internal readonly Dictionary<AssetLocation, ItemRemapEntry> _itemRemap = [];

  protected override void OnStartedServer(ICoreServerAPI api)
  {
    // Migrated blocks can also sit as item stacks in a player's inventory (the chunk scan never
    // sees those), so remap them on join.
    api.Event.PlayerJoin += OnPlayerJoin;
  }

  /// <summary>Builds the remap tables on first use; returns false if nothing in this world matches.</summary>
  protected override bool BuildWork()
  {
    BuildRemapTable();
    return _remap.Count > 0 || _itemRemap.Count > 0;
  }

  /// <summary>Matches one placed cell against the block remap table and rewrites it if it hits.</summary>
  protected override int VisitCell(IBlockAccessor ba, BlockPos pos, int blockId)
  {
    // Resolve the live block and match on its code, so renumbered ids and missing-block
    // placeholders are both handled.
    Block block = _sapi.World.GetBlock(blockId);
    if (
      block?.Code == null
      || !_remap.TryGetValue(block.Code, out RemapEntry entry)
    )
      return 0;

    ReplaceBlock(ba, pos, entry);
    return 1;
  }

  /// <summary>
  /// Rewrites migrated blocks held as item stacks in this chunk's container block entities (chests,
  /// ground storage, mold racks) - stacks the voxel loop never sees.
  /// </summary>
  protected override int VisitChunkEntities(IWorldChunk chunk)
  {
    if (chunk.BlockEntities == null)
      return 0;

    int migrated = 0;
    // Snapshot the values first - VisitCell's ReplaceBlock may have mutated this collection.
    foreach (BlockEntity be in chunk.BlockEntities.Values.ToArray())
      if (be is IBlockEntityContainer { Inventory: { } inv })
      {
        int n = RemapInventory(inv);
        if (n > 0)
        {
          be.MarkDirty(true);
          migrated += n;
        }
      }

    return migrated;
  }

  private void LogColumn(int migrated, int chunkX, int chunkZ) =>
    _sapi.Logger.Notification(
      Tag + " Migrated {0} block(s)/stack(s) in chunk column {1},{2}.",
      migrated,
      chunkX,
      chunkZ
    );

  protected override void OnColumnSwept(int chunkX, int chunkZ, int migrated) =>
    LogColumn(migrated, chunkX, chunkZ);

  protected override void OnColumnStreamedIn(int chunkX, int chunkZ, int migrated) =>
    LogColumn(migrated, chunkX, chunkZ);

  protected override void OnStartupSweepComplete(int total) =>
    _sapi.Logger.Notification(
      Tag + " Startup migration sweep updated {0} block(s) across loaded chunks.",
      total
    );

  /// <summary>
  /// Rewrites every item stack in <paramref name="inv"/> whose collectible is a migration source to
  /// its replacement, preserving stack size and attributes (e.g. a filled mold's stored contents).
  /// Block stacks use the block table (and can be removed); item stacks use the item table. A code
  /// that is both a block and an item is resolved by the stack's class. Returns how many slots changed.
  /// </summary>
  internal int RemapInventory(IInventory inv)
  {
    int changed = 0;
    foreach (ItemSlot slot in inv)
    {
      ItemStack? stack = slot.Itemstack;
      if (stack?.Collectible?.Code == null)
        continue;

      bool isBlock = stack.Class == EnumItemClass.Block;
      ItemStack? replacement;

      if (isBlock)
      {
        if (!_remap.TryGetValue(stack.Collectible.Code, out RemapEntry entry))
          continue;
        // A removal: drop the stack from the slot entirely.
        if (entry.NewBlock == null)
        {
          slot.Itemstack = null;
          slot.MarkDirty();
          changed++;
          continue;
        }
        replacement = new ItemStack(entry.NewBlock, stack.StackSize);
      }
      else
      {
        if (!_itemRemap.TryGetValue(stack.Collectible.Code, out ItemRemapEntry entry))
          continue;
        replacement = new ItemStack(entry.NewItem, stack.StackSize);
      }

      if (stack.Attributes is { Count: > 0 })
        replacement.Attributes = stack.Attributes.Clone();
      slot.Itemstack = replacement;
      slot.MarkDirty();
      changed++;
    }
    return changed;
  }

  /// <summary>Remaps any migrated blocks a joining player is carrying as item stacks.</summary>
  private void OnPlayerJoin(IServerPlayer player)
  {
    // Shares the base's single-build guard with the chunk sweep, so joining before any column loads
    // still builds the tables (and only once).
    if (!EnsureInitialized())
      return;

    int changed = 0;
    foreach (
      KeyValuePair<string, IInventory> kv in player.InventoryManager.Inventories
    )
    {
      // The creative inventory is a virtual search list whose Count getter NREs on join - skip it.
      if (
        kv.Value is not { } inv
        || inv.ClassName == GlobalConstants.creativeInvClassName
      )
        continue;

      // A single misbehaving (e.g. modded) inventory must not abort the join.
      try
      {
        changed += RemapInventory(inv);
      }
      catch (Exception e)
      {
        _sapi.Logger.Warning(
          Tag + " Skipped inventory '{0}' for {1} during migration: {2}",
          kv.Key,
          player.PlayerName,
          e.Message
        );
      }
    }

    if (changed > 0)
      _sapi.Logger.Notification(
        Tag + " Migrated {0} carried item stack(s) for {1}.",
        changed,
        player.PlayerName
      );
  }

  private void BuildRemapTable()
  {
    foreach (IBlockCodeMigration migration in Discover<IBlockCodeMigration>())
    {
      var beMigration = migration as IBlockEntityMigration;
      int count = 0;
      foreach (var (oldCode, newCode) in migration.GetRemaps(_sapi))
      {
        // GetBlock resolves missing-block placeholders too, so a null means this world has no
        // such legacy block - skip it.
        if (_sapi.World.GetBlock(oldCode) == null)
          continue;

        Block? newBlock = _sapi.World.GetBlock(newCode);
        if (newBlock == null || newBlock.BlockId == 0)
        {
          _sapi.Logger.Warning(
            Tag
              + " Migration '{0}': replacement block '{1}' is not registered; skipping.",
            migration.Name,
            newCode
          );
          continue;
        }

        if (
          _remap.TryGetValue(oldCode, out RemapEntry existing)
          && existing.NewBlock?.Code.Equals(newCode) != true
        )
        {
          _sapi.Logger.Warning(
            Tag
              + " Migration '{0}' remaps {1} but it is already mapped elsewhere; keeping the first mapping.",
            migration.Name,
            oldCode
          );
          continue;
        }

        _remap[oldCode] = new RemapEntry(
          newBlock,
          oldCode,
          newCode,
          beMigration
        );
        count++;
      }

      if (count > 0)
        _sapi.Logger.Notification(
          Tag + " Migration '{0}': {1} legacy block code(s) found to update.",
          migration.Name,
          count
        );
    }

    // Purges (IBlockRemoval): same matching, but the action is "delete" (null replacement).
    foreach (IBlockRemoval removal in Discover<IBlockRemoval>())
    {
      int count = 0;
      foreach (AssetLocation code in removal.GetRemovals(_sapi))
      {
        if (code == null || _sapi.World.GetBlock(code) == null)
          continue;

        if (_remap.ContainsKey(code))
        {
          _sapi.Logger.Warning(
            Tag
              + " Removal '{0}' targets {1} but it is already mapped elsewhere; keeping the existing mapping.",
            removal.Name,
            code
          );
          continue;
        }

        _remap[code] = new RemapEntry(null, code, null, null);
        count++;
      }

      if (count > 0)
        _sapi.Logger.Notification(
          Tag + " Removal '{0}': {1} block code(s) marked for purge.",
          removal.Name,
          count
        );
    }

    // Item migrations: rewrites for held stacks only (items are never in the world voxel grid).
    foreach (IItemCodeMigration migration in Discover<IItemCodeMigration>())
    {
      int count = 0;
      foreach (var (oldCode, newCode) in migration.GetRemaps(_sapi))
      {
        // Old code must resolve as an item in this world (missing-item placeholder included).
        if (_sapi.World.GetItem(oldCode) == null)
          continue;

        Item? newItem = _sapi.World.GetItem(newCode);
        if (newItem == null || newItem.ItemId == 0)
        {
          _sapi.Logger.Warning(
            Tag
              + " Item migration '{0}': replacement item '{1}' is not registered; skipping.",
            migration.Name,
            newCode
          );
          continue;
        }

        if (
          _itemRemap.TryGetValue(oldCode, out ItemRemapEntry existing)
          && !existing.NewCode.Equals(newCode)
        )
        {
          _sapi.Logger.Warning(
            Tag
              + " Item migration '{0}' remaps {1} but it is already mapped elsewhere; keeping the first mapping.",
            migration.Name,
            oldCode
          );
          continue;
        }

        _itemRemap[oldCode] = new ItemRemapEntry(newItem, oldCode, newCode);
        count++;
      }

      if (count > 0)
        _sapi.Logger.Notification(
          Tag + " Item migration '{0}': {1} legacy item code(s) found to update.",
          migration.Name,
          count
        );
    }
  }

  /// <summary>
  /// Swaps the block at <paramref name="pos"/> for its replacement. A plain migration is a bare
  /// <c>SetBlock</c>; one that handles BE state captures the old entity's tree first and applies it
  /// to the new entity afterwards.
  /// </summary>
  internal void ReplaceBlock(IBlockAccessor ba, BlockPos pos, RemapEntry entry)
  {
    // A removal: delete the block (and its entity) outright.
    if (entry.NewBlock == null)
    {
      ba.SetBlock(0, pos);
      return;
    }

    if (entry.BlockEntityMigration == null)
    {
      ba.SetBlock(entry.NewBlock.BlockId, pos);
      return;
    }

    ITreeAttribute? oldState = null;
    if (ba.GetBlockEntity(pos) is BlockEntity oldBe)
    {
      oldState = new TreeAttribute();
      oldBe.ToTreeAttributes(oldState);
    }

    ba.SetBlock(entry.NewBlock.BlockId, pos);

    if (ba.GetBlockEntity(pos) is BlockEntity newBe)
    {
      entry.BlockEntityMigration.MigrateBlockEntity(
        entry.OldCode,
        entry.NewCode!, // non-null for a migration entry (removals return above)
        oldState,
        newBe,
        _sapi.World
      );
      newBe.MarkDirty(true);
    }
  }

  // Scan every loaded assembly for parameterless implementations of T: this system lives in exlib,
  // but lpex/smex declare their own migrations and removals.
  private static IEnumerable<T> Discover<T>()
    where T : class
  {
    var found = new List<Type>();
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
      Type[] types;
      try
      {
        types = asm.GetTypes();
      }
      catch (ReflectionTypeLoadException ex)
      {
        types = ex.Types.Where(t => t != null).ToArray()!;
      }

      foreach (var t in types)
      {
        if (
          !typeof(T).IsAssignableFrom(t)
          || t is not { IsAbstract: false, IsInterface: false }
          || t.GetConstructor(Type.EmptyTypes) == null
        )
          continue;
        found.Add(t);
      }
    }

    // Deterministic order so the "keep the first mapping" conflict resolution above is stable across
    // runs - AppDomain.GetAssemblies() order is not guaranteed, so first-seen-wins was previously
    // load-order dependent.
    foreach (
      var t in found
        .OrderBy(t => t.Assembly.FullName, StringComparer.Ordinal)
        .ThenBy(t => t.FullName, StringComparer.Ordinal)
    )
      yield return (T)Activator.CreateInstance(t)!;
  }
}
