using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// Declares block codes purged from the world: deleted where they sit, and stripped from any container
/// or player inventory holding them as an item stack. Used to remove content a mod no longer wants
/// present, e.g. behind a config toggle. Implementations need a public parameterless constructor;
/// <see cref="BlockMigrationModSystem"/> discovers every one and applies the removals through the same
/// chunk-and-inventory sweep it uses for <see cref="IBlockCodeMigration"/>, as chunks load and players
/// join.
/// </summary>
public interface IBlockRemoval {
  /// <summary>Short human-readable name, used only for log output.</summary>
  string Name { get; }

  /// <summary>
  /// Full, domain-qualified codes of blocks to delete from the world and from inventories.
  /// <paramref name="api"/> allows enumerating variants or reading config. Codes absent in this world
  /// are skipped, so returning a superset is safe. Any enabling condition is read here: the purge
  /// table is built once at startup, so a change takes effect on the next world load.
  /// </summary>
  IEnumerable<AssetLocation> GetRemovals(ICoreServerAPI api);
}
