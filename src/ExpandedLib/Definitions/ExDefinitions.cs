using System.Collections.Generic;
using System.Text;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Process-wide registry of code-first block definitions. A mod authors a block in C# with
/// <see cref="ExBlockDef"/> and registers it here (from its <c>ModSystem.Start</c>); the shared
/// <see cref="ExDefinitionModSystem"/> serializes each and injects it as a synthetic <c>blocktypes/</c>
/// asset on the server, before the object loader runs. Keyed by asset location so a re-register (or a
/// deliberate override) replaces rather than duplicates.
/// </summary>
public static class ExDefinitions
{
  private static readonly ExKeyedRegistry<ExBlockDef> _blocks = new(d =>
    d.Location.ToString()
  );

  /// <summary>Registers (or replaces) a code-first block definition.</summary>
  public static void RegisterBlock(ExBlockDef def) => _blocks.Register(def);

  /// <summary>Every registered block definition.</summary>
  public static IReadOnlyCollection<ExBlockDef> Blocks => _blocks.Values;

  /// <summary>Drops every registered definition (used by tests to isolate the static registry).</summary>
  public static void Clear() => _blocks.Clear();

  /// <summary>
  /// Serializes every registered block definition to the synthetic assets the loader consumes:
  /// one <c>{domain}:blocktypes/{code}.json</c> per def, its bytes the def's JSON. Pure and
  /// side-effect-free, so the whole injection pipeline is unit-testable up to the
  /// <c>AssetManager.Add</c> sink. The <paramref name="origin"/> is stamped as each asset's
  /// <see cref="IAsset.Origin"/>.
  /// </summary>
  public static IEnumerable<(AssetLocation location, IAsset asset)> BuildBlockAssets(
    IAssetOrigin origin
  )
  {
    foreach (ExBlockDef def in _blocks.Values)
    {
      // Parameterless ToString() (indented JSON) - the payload only needs to be valid JSON for the
      // loader to parse; whitespace is irrelevant. Avoids the Formatting overload, which the game's
      // bundled Newtonsoft build does not expose at runtime.
      byte[] bytes = Encoding.UTF8.GetBytes(def.ToJson().ToString());
      yield return (
        def.Location,
        ExSyntheticAsset.Create(def.Location, bytes, origin)
      );
    }
  }
}
