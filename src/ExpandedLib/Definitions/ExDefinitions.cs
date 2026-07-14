using System;
using System.Collections.Generic;
using System.Reflection;
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
  /// Builds a <c>type -&gt; orientation states</c> map from a class's code-first defs - the single
  /// source a block derives its runtime <c>AllowedOrientations</c> from, so the orientation list lives
  /// only in the variant groups (never a hand-kept duplicate that drifts). Defs without a single
  /// <c>type</c> state (e.g. a worldproperty-oriented block) are skipped.
  /// </summary>
  public static Dictionary<string, string[]> OrientationMap(
    IEnumerable<ExBlockDef> defs
  )
  {
    var map = new Dictionary<string, string[]>();
    foreach (ExBlockDef d in defs)
    {
      string[] types = d.VariantStates("type");
      if (types.Length == 1)
        map[types[0]] = d.VariantStates("orientation");
    }
    return map;
  }

  /// <summary>
  /// Scans <paramref name="asm"/> for <see cref="IExBlockDefProvider"/> classes and registers each
  /// one's co-located definition (built for <paramref name="domain"/>, the mod id). Called from
  /// <see cref="Registries.Entities.EntityRegistry.RegisterAll"/> so a mod's block defs are discovered
  /// alongside its class registration. Returns how many were registered.
  /// </summary>
  public static int DiscoverAndRegister(string domain, Assembly asm)
  {
    int count = 0;
    foreach (Type type in ReflectionScan.GetCandidateTypes(asm))
      foreach (ExBlockDef def in DefinitionsOf(type, domain))
      {
        RegisterBlock(def);
        count++;
      }
    return count;
  }

  /// <summary>
  /// The code-first defs a <paramref name="type"/> declares itself, or empty when it declares none.
  /// Used both by discovery and by a block deriving runtime tables from its own def
  /// (<c>ExDefinitions.OrientationMap(DefinitionsOf(GetType(), domain))</c>).
  /// <para>
  /// DeclaredOnly matters: several blocks subclass a def-providing base (the special pipes extend
  /// BlockPipe), and without it a derived class would return the base's inherited defs. A class
  /// contributes only the defs it declares itself.
  /// </para>
  /// </summary>
  public static IEnumerable<ExBlockDef> DefinitionsOf(Type type, string domain)
  {
    if (!typeof(IExBlockDefProvider).IsAssignableFrom(type))
      return [];

    // The static abstract Definitions is implemented as a public static method on the concrete class.
    MethodInfo? define = type.GetMethod(
      nameof(IExBlockDefProvider.Definitions),
      BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
      binder: null,
      types: [typeof(string)],
      modifiers: null
    );
    return define?.Invoke(null, [domain]) as IEnumerable<ExBlockDef> ?? [];
  }

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
