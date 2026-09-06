using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Implemented by a module (or main assembly) entry point that emits code-first definitions
/// depending on assets that are only readable once every mod's <c>Start</c> has run - a metal
/// catalogue, a config-driven item family. <see cref="Registries.EntityRegistry.RegisterAll"/>
/// discovers implementors alongside a mod's classes; <see cref="ExDefinitions.RunContributors"/>
/// instantiates and runs each one at <c>AssetsLoaded</c> 0.04, right before injection, so the result
/// is correct regardless of which host or module order discovered it. Runs on the server only - the
/// definition system does not exist client-side.
/// </summary>
public interface IExDefinitionContributor {
  /// <summary>Registers this contributor's definitions through <see cref="ExDefinitions"/>'s
  /// <c>Register*</c> methods.</summary>
  void Contribute(ICoreAPI api);
}
