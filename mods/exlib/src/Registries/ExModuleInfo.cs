using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExpandedLib.Registries;

/// <summary>
/// One module discovered by <see cref="ExModules"/>, built once per process from an assembly
/// carrying <c>[assembly: ExModule]</c>.
/// </summary>
public sealed class ExModuleInfo {
  /// <summary>The module's id, unique across every host.</summary>
  public required string Id { get; init; }

  /// <summary>The mod id whose lifecycle drives this module.</summary>
  public required string Host { get; init; }

  /// <summary>The Vintage Story mod id that ships this assembly; <see cref="ExModules.For"/> keeps
  /// only the modules whose <see cref="Mod"/> is enabled on the world it is asked about.</summary>
  public required string Mod { get; init; }

  /// <summary>Module ids this one runs after, within the same host.</summary>
  public required IReadOnlyList<string> Requires { get; init; }

  /// <summary>The module's own assembly.</summary>
  public required Assembly Assembly { get; init; }

  /// <summary>Concrete <see cref="IExModule"/> implementors in <see cref="Assembly"/>, name order.
  /// A type with no parameterless constructor is left out; see <see cref="ExModules.All"/>.</summary>
  public required IReadOnlyList<Type> EntryPoints { get; init; }

  /// <summary>Whether the host patches this assembly's uncategorised Harmony classes.</summary>
  public bool PatchHarmony { get; init; }

  /// <summary>The id this module's Harmony patches are applied and removed under: <see cref="Host"/>
  /// and <see cref="Id"/> joined with a dot, distinct from any other module's or the host's own.</summary>
  public string HarmonyId => Host + "." + Id;
}

/// <summary>One host's modules, ordered by <see cref="ExModules.Order"/>, and the discovery or
/// ordering errors that excluded any of them.</summary>
/// <param name="Modules">The host's modules, dependency order.</param>
/// <param name="Errors">One line per module (or group of modules) excluded, or per duplicate id
/// found. Empty when nothing was excluded.</param>
public sealed record ExModuleSet(
  IReadOnlyList<ExModuleInfo> Modules,
  IReadOnlyList<string> Errors
);
