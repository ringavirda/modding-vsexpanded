using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// The item counterpart of <see cref="IBlockCodeMigration"/>: declares how item codes from an older
/// version of the mod should be rewritten to their current equivalents. Implement this (with a public
/// parameterless constructor) when an item is renamed or moved domain;
/// <see cref="BlockMigrationModSystem"/> auto-discovers every implementation and rewrites matching
/// item stacks wherever they are held - container block entities, ground storage and player
/// inventories.
/// <para>
/// Unlike blocks, items are never placed in the world voxel grid, so item migrations only ever touch
/// item stacks. A code that exists as <em>both</em> a block and an item (e.g. <c>slag</c>) is
/// disambiguated by the stack's class, so a block stack and an item stack sharing a code migrate
/// independently.
/// </para>
/// </summary>
public interface IItemCodeMigration
{
  /// <summary>Short human-readable name, used only for log output.</summary>
  string Name { get; }

  /// <summary>
  /// Returns <c>(oldCode, newCode)</c> pairs of full, domain-qualified item codes. <paramref name="api"/>
  /// is provided so implementations can enumerate variants programmatically. Old codes are the ones
  /// that no longer resolve; each new code must be a currently registered item. Pairs whose old or new
  /// code is absent in this world are skipped, so it is safe to return the full set unconditionally.
  /// </summary>
  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  );
}
