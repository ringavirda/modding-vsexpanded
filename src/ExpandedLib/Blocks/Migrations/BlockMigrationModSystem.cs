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
/// Server-side world migrator for renamed or re-variantted blocks and items, and a purger for codes a
/// mod drops. Collects every <see cref="IBlockCodeMigration"/>, <see cref="IItemCodeMigration"/> and
/// <see cref="IBlockRemoval"/> in all loaded assemblies into legacy-code → action tables, then applies
/// them as chunk columns load (the walk lives in <see cref="ChunkColumnSweeperModSystem"/>). Matching
/// is on <see cref="RegistryObject.Code"/>, not a precomputed id, because the engine renumbers ids on load; that
/// also catches the missing-block placeholders kept for removed codes. A plain migration is a bare
/// block-id swap, one implementing <see cref="IBlockEntityMigration"/> also gets the old BE's tree, a
/// removal deletes in place; matching stacks in container BEs and inventories are rewritten either way.
/// </summary>
public class BlockMigrationModSystem : ChunkColumnSweeperModSystem {
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

  protected override void OnStartedServer(ICoreServerAPI api) {
    // Migrated blocks can also sit as item stacks in a player's inventory (the chunk scan never
    // sees those), so remap them on join.
    api.Event.PlayerJoin += OnPlayerJoin;
  }

  /// <summary>Builds the remap tables on first use; returns false if nothing in this world matches.</summary>
  protected override bool BuildWork() {
    BuildRemapTable();
    return _remap.Count > 0 || _itemRemap.Count > 0;
  }

  /// <summary>Matches one placed cell against the block remap table and rewrites it if it hits.</summary>
  protected override int VisitCell(IBlockAccessor ba, BlockPos pos, int blockId) {
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
  protected override int VisitChunkEntities(IWorldChunk chunk) {
    if (chunk.BlockEntities == null)
      return 0;

    int migrated = 0;
    // Snapshot the values first - VisitCell's ReplaceBlock may have mutated this collection.
    foreach (BlockEntity be in chunk.BlockEntities.Values.ToArray())
      if (be is IBlockEntityContainer { Inventory: { } inv }) {
        int n = RemapInventory(inv);
        if (n > 0) {
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

  protected override void OnColumnStreamedIn(
    int chunkX,
    int chunkZ,
    int migrated
  ) => LogColumn(migrated, chunkX, chunkZ);

  protected override void OnStartupSweepComplete(int total) =>
    _sapi.Logger.Notification(
      Tag
        + " Startup migration sweep updated {0} block(s) across loaded chunks.",
      total
    );

  /// <summary>
  /// Rewrites every item stack in <paramref name="inv"/> whose collectible is a migration source,
  /// preserving stack size and attributes (e.g. a filled mold's stored contents). Block stacks use the
  /// block table and can be removed, item stacks the item table; a code that is both resolves by the
  /// stack's class. Returns how many slots changed.
  /// </summary>
  internal int RemapInventory(IInventory inv) {
    int changed = 0;
    foreach (ItemSlot slot in inv) {
      ItemStack? stack = slot.Itemstack;
      if (stack?.Collectible?.Code == null)
        continue;

      bool isBlock = stack.Class == EnumItemClass.Block;
      ItemStack? replacement;

      if (isBlock) {
        if (!_remap.TryGetValue(stack.Collectible.Code, out RemapEntry entry))
          continue;
        // A removal: drop the stack from the slot entirely.
        if (entry.NewBlock == null) {
          slot.Itemstack = null;
          slot.MarkDirty();
          changed++;
          continue;
        }
        replacement = new ItemStack(entry.NewBlock, stack.StackSize);
      } else {
        if (
          !_itemRemap.TryGetValue(
            stack.Collectible.Code,
            out ItemRemapEntry entry
          )
        )
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
  private void OnPlayerJoin(IServerPlayer player) {
    // Shares the base's single-build guard with the chunk sweep, so joining before any column loads
    // still builds the tables (and only once).
    if (!EnsureInitialized())
      return;

    int changed = 0;
    foreach (
      KeyValuePair<string, IInventory> kv in player.InventoryManager.Inventories
    ) {
      // The creative inventory is a virtual search list whose Count getter NREs on join - skip it.
      if (
        kv.Value is not { } inv
        || inv.ClassName == GlobalConstants.creativeInvClassName
      )
        continue;

      // A single misbehaving (e.g. modded) inventory must not abort the join.
      try {
        changed += RemapInventory(inv);
      } catch (Exception e) {
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

  /// <summary>One declared block remap, before any resolution against the world.</summary>
  public readonly record struct DeclaredRemap(
    string Migration,
    AssetLocation OldCode,
    AssetLocation NewCode
  );

  /// <summary>
  /// Every <c>(oldCode, newCode)</c> pair declared by any discovered <see cref="IBlockCodeMigration"/>,
  /// before resolution against the world. Exposed so coverage of released codes can be checked across
  /// all migrations at once.
  /// </summary>
  public static IEnumerable<DeclaredRemap> DeclaredBlockRemaps(
    ICoreServerAPI api
  ) {
    foreach (IBlockCodeMigration migration in Discover<IBlockCodeMigration>())
      foreach (var (oldCode, newCode) in migration.GetRemaps(api))
        yield return new DeclaredRemap(migration.Name, oldCode, newCode);
  }

  /// <summary>Every block code any discovered <see cref="IBlockRemoval"/> declares for purging. A
  /// released code listed here counts as covered: a deliberate deletion is not an orphan.</summary>
  public static IEnumerable<(
    string Removal,
    AssetLocation Code
  )> DeclaredRemovals(ICoreServerAPI api) {
    foreach (IBlockRemoval removal in Discover<IBlockRemoval>())
      foreach (AssetLocation code in removal.GetRemovals(api))
        if (code != null)
          yield return (removal.Name, code);
  }

  /// <summary>Hop limit for the chain walk; guards a cyclic declaration (A→B→A).</summary>
  internal const int MaxChainHops = 16;

  /// <summary>
  /// Follows the declared remap graph from <paramref name="from"/> to the code it finally lands on.
  /// Pure: no world, registry or discovery access. Intermediate codes are dead by construction, so
  /// only the terminal can be resolved against the world.
  /// </summary>
  /// <param name="next">The next hop for a code, or null when it is terminal.</param>
  /// <param name="isPurged">Whether a code is declared for removal; a purge terminates the chain.</param>
  /// <param name="purged">Set when the walk ended on a declared removal.</param>
  /// <param name="overflowed">Set when <see cref="MaxChainHops"/> was hit: a cycle, or an over-long
  /// chain.</param>
  // System.Func spelled out: Vintagestory.API.Common declares its own Func<,> and the two are
  // ambiguous in this file.
  internal static AssetLocation FollowChain(
    AssetLocation from,
    System.Func<AssetLocation, AssetLocation?> next,
    System.Func<AssetLocation, bool> isPurged,
    out bool purged,
    out bool overflowed
  ) {
    AssetLocation cursor = from;
    purged = false;
    overflowed = false;

    for (int hops = 0; ; hops++) {
      // Ask for the next hop before testing the limit. Testing first makes a chain of exactly
      // MaxChainHops report an overflow even though it terminates cleanly - the guard is for a cycle,
      // not for a long-but-finite rename history.
      AssetLocation? hop = next(cursor);
      if (hop == null)
        return cursor;

      if (hops >= MaxChainHops) {
        overflowed = true;
        return cursor;
      }

      cursor = hop;
      if (isPurged(cursor)) {
        purged = true;
        return cursor;
      }
    }
  }

  private void BuildRemapTable() {
    // ── Pass 1: collect every declared pair without resolving it against the world. ──────────────
    // A chain's intermediate codes are dead by construction: in ppex:x → lpex:x → iiex:x, lpex:x is
    // precisely the code that no longer registers, so resolving while collecting would honour one hop.
    var declared =
      new Dictionary<
        AssetLocation,
        (AssetLocation New, IBlockEntityMigration? Be, string Name)
      >();

    foreach (IBlockCodeMigration migration in Discover<IBlockCodeMigration>()) {
      var beMigration = migration as IBlockEntityMigration;
      foreach (var (oldCode, newCode) in migration.GetRemaps(_sapi)) {
        if (oldCode == null || newCode == null || oldCode.Equals(newCode))
          continue;

        if (declared.TryGetValue(oldCode, out var existing)) {
          if (!existing.New.Equals(newCode))
            _sapi.Logger.Warning(
              Tag
                + " Migration '{0}' remaps {1} but it is already mapped elsewhere; keeping the first mapping.",
              migration.Name,
              oldCode
            );
          continue;
        }

        declared[oldCode] = (newCode, beMigration, migration.Name);
      }
    }

    // ── Pass 2: declared purges. A removal terminates a chain just as a live block does. ─────────
    var removals = new Dictionary<AssetLocation, string>();
    foreach (IBlockRemoval removal in Discover<IBlockRemoval>())
      foreach (AssetLocation code in removal.GetRemovals(_sapi))
        if (code != null && !declared.ContainsKey(code))
          removals.TryAdd(code, removal.Name);

    // ── Pass 3: follow each declared source to its terminal, then resolve that once. ─────────────
    var perMigration = new Dictionary<string, int>();
    foreach (AssetLocation oldCode in declared.Keys) {
      // GetBlock resolves missing-block placeholders too, so a null means this world has no such
      // legacy block - nothing to migrate.
      if (_sapi.World.GetBlock(oldCode) == null)
        continue;

      // The block-entity migration is the first hop's; mid-chain hops are not consulted, so a hop that
      // needs to reshape state must be declared as a direct pair.
      var (_, beMigration, sourceName) = declared[oldCode];

      AssetLocation cursor = FollowChain(
        oldCode,
        c => declared.TryGetValue(c, out var hop) ? hop.New : null,
        removals.ContainsKey,
        out bool purged,
        out bool overflowed
      );

      if (overflowed)
        _sapi.Logger.Warning(
          Tag
            + " Migration chain from '{0}' exceeded {1} hops; stopping at '{2}'.",
          oldCode,
          MaxChainHops,
          cursor
        );

      if (purged) {
        _remap[oldCode] = new RemapEntry(null, oldCode, null, null);
        perMigration[sourceName] =
          perMigration.GetValueOrDefault(sourceName) + 1;
        continue;
      }

      Block? newBlock = _sapi.World.GetBlock(cursor);
      if (newBlock == null || newBlock.BlockId == 0) {
        _sapi.Logger.Warning(
          Tag
            + " Migration '{0}': {1} resolves to '{2}', which is not registered; skipping.",
          sourceName,
          oldCode,
          cursor
        );
        continue;
      }

      _remap[oldCode] = new RemapEntry(newBlock, oldCode, cursor, beMigration);
      perMigration[sourceName] = perMigration.GetValueOrDefault(sourceName) + 1;
    }

    foreach (var (name, count) in perMigration)
      _sapi.Logger.Notification(
        Tag + " Migration '{0}': {1} legacy block code(s) found to update.",
        name,
        count
      );

    // Purges declared directly on a live code (not reached through a chain).
    foreach (var (code, removalName) in removals) {
      if (_sapi.World.GetBlock(code) == null || _remap.ContainsKey(code))
        continue;

      _remap[code] = new RemapEntry(null, code, null, null);
      _sapi.Logger.Notification(
        Tag + " Removal '{0}': block code {1} marked for purge.",
        removalName,
        code
      );
    }

    // Item migrations: rewrites for held stacks only (items are never in the world voxel grid).
    foreach (IItemCodeMigration migration in Discover<IItemCodeMigration>()) {
      int count = 0;
      foreach (var (oldCode, newCode) in migration.GetRemaps(_sapi)) {
        // Old code must resolve as an item in this world (missing-item placeholder included).
        if (_sapi.World.GetItem(oldCode) == null)
          continue;

        Item? newItem = _sapi.World.GetItem(newCode);
        if (newItem == null || newItem.ItemId == 0) {
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
        ) {
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
          Tag
            + " Item migration '{0}': {1} legacy item code(s) found to update.",
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
  internal void ReplaceBlock(IBlockAccessor ba, BlockPos pos, RemapEntry entry) {
    // A removal: delete the block (and its entity) outright.
    if (entry.NewBlock == null) {
      ba.SetBlock(0, pos);
      return;
    }

    if (entry.BlockEntityMigration == null) {
      ba.SetBlock(entry.NewBlock.BlockId, pos);
      return;
    }

    ITreeAttribute? oldState = null;
    if (ba.GetBlockEntity(pos) is BlockEntity oldBe) {
      oldState = new TreeAttribute();
      oldBe.ToTreeAttributes(oldState);
    }

    ba.SetBlock(entry.NewBlock.BlockId, pos);

    if (ba.GetBlockEntity(pos) is BlockEntity newBe) {
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
  // but iiex/siex declare their own migrations and removals.
  private static IEnumerable<T> Discover<T>()
    where T : class {
    var found = new List<Type>();
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
      Type[] types;
      try {
        types = asm.GetTypes();
      } catch (ReflectionTypeLoadException ex) {
        types = ex.Types.Where(t => t != null).ToArray()!;
      }

      foreach (var t in types) {
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
    // runs: AppDomain.GetAssemblies() order is not guaranteed.
    foreach (
      var t in found
        .OrderBy(t => t.Assembly.FullName, StringComparer.Ordinal)
        .ThenBy(t => t.FullName, StringComparer.Ordinal)
    )
      yield return (T)Activator.CreateInstance(t)!;
  }
}
