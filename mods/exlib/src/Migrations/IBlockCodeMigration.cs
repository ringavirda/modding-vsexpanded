using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Migrations;

/// <summary>
/// Declares how block codes from an older version of a mod are rewritten to their current
/// equivalents, for a renamed or re-varianted block. Implementations need a public parameterless
/// constructor; <see cref="BlockMigrationModSystem"/> discovers every one and rewrites previously
/// placed instances - which load as "missing" placeholder blocks that keep their original code -
/// in-world as chunks load.
/// </summary>
public interface IBlockCodeMigration {
  /// <summary>Short human-readable name, used only for log output.</summary>
  string Name { get; }

  /// <summary>
  /// <c>(oldCode, newCode)</c> pairs of full, domain-qualified block codes. Old codes are the ones
  /// that no longer resolve; each new code must be a currently registered block. Pairs whose old or
  /// new code is absent in this world are skipped, so returning the full set unconditionally is safe.
  /// <paramref name="api"/> allows enumerating variants programmatically.
  /// </summary>
  IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  );
}
