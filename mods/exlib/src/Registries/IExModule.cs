using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Registries;

/// <summary>
/// An entry point of a module: an assembly carrying <c>[assembly: ExModule]</c>. The host names in
/// <c>Host</c> runs it through the phases of its own <see cref="ModSystem"/>, in the order
/// <see cref="ExModules.Order"/> gives the module among the rest of the host's, after registering the
/// assembly's classes for it. Every method has an empty default, so a module overrides only the
/// phases it needs. See the wiki's Modules page.
/// </summary>
public interface IExModule {
  /// <summary>Runs in the host's <c>StartPre</c>, before any registration.</summary>
  void StartPre(ICoreAPI api) { }

  /// <summary>Runs in the host's <c>Start</c>, after this module's registered classes have been
  /// registered for it.</summary>
  void Start(ICoreAPI api) { }

  /// <summary>Runs in the host's <c>StartServerSide</c>, after this module's server commands have
  /// been registered.</summary>
  void StartServerSide(ICoreServerAPI api) { }

  /// <summary>Runs in the host's <c>StartClientSide</c>, after this module's preferences and client
  /// commands have been registered.</summary>
  void StartClientSide(ICoreClientAPI api) { }

  /// <summary>
  /// Runs in the host's <c>AssetsLoaded</c>: assets are readable here and a catalogue read belongs
  /// in this phase, but it is not where a definition is contributed - a module that emits
  /// code-first definitions from loaded assets implements
  /// <see cref="Definitions.IExDefinitionContributor"/> instead, which runs after every module's
  /// <c>Start</c> and before injection regardless of host order.
  /// </summary>
  void AssetsLoaded(ICoreAPI api) { }

  /// <summary>
  /// Runs in the host's <c>AssetsFinalize</c>, after the asset-patch pipeline has merged every mod's
  /// JSON. A module loading a catalogue does it here.
  /// </summary>
  void AssetsFinalize(ICoreAPI api) { }

  /// <summary>Runs when the host disposes.</summary>
  void Dispose() { }
}
