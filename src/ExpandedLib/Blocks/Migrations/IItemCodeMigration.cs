using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// The item counterpart of <see cref="IBlockCodeMigration"/>: declares how item codes from an older
/// version of a mod are rewritten to their current equivalents, for an item that was renamed or moved
/// domain. Implementations need a public parameterless constructor;
/// <see cref="BlockMigrationModSystem"/> discovers every one and rewrites matching item stacks wherever
/// they are held - container block entities, ground storage and player inventories. Items are never
/// placed in the world voxel grid, so item migrations only ever touch item stacks. A code that exists
/// as both a block and an item (e.g. <c>slag</c>) is disambiguated by the stack's class, so a block
/// stack and an item stack sharing a code migrate independently.
/// </summary>
public interface IItemCodeMigration {
  /// <summary>Short human-readable name, used only for log output.</summary>
  string Name { get; }

  /// <summary>
  /// <c>(oldCode, newCode)</c> pairs of full, domain-qualified item codes. Old codes are the ones that
  /// no longer resolve; each new code must be a currently registered item. Pairs whose old or new code
  /// is absent in this world are skipped, so returning the full set unconditionally is safe.
  /// <paramref name="api"/> allows enumerating variants programmatically.
  /// </summary>
  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  );
}
