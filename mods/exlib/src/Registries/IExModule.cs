using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// The entry point of a companion assembly: a second dll inside one mod's folder, which cannot
/// carry mod systems of its own. The mod's <see cref="ExModSystem"/> runs it through the same
/// lifecycle phases in <see cref="Order"/> order, after registering its classes for it.
/// </summary>
/// <remarks>
/// Only one dll per mod folder may contain mod systems at all, however many it declares; a second
/// one that does makes the game refuse the whole mod. The assembly must carry
/// <c>[assembly: ExDomain("&lt;modid&gt;")]</c>, which is what says whose module it is. Every method
/// has an empty default, so a module overrides only the phases it needs. See the wiki's Registries
/// page, "Shipping more than one assembly".
/// </remarks>
public interface IExModule {
  /// <summary>
  /// Order among the modules of one mod, lowest first; ties keep discovery order, which is the
  /// order the runtime happens to have loaded the assemblies in and so is not worth relying on.
  /// Defaults to 0.1, matching a <see cref="ModSystem"/>'s own default execute order.
  /// </summary>
  double Order => 0.1;

  /// <summary>Runs in the owning mod's <c>StartPre</c>, before any registration.</summary>
  void StartPre(ICoreAPI api) { }

  /// <summary>
  /// Runs in the owning mod's <c>Start</c>, after this assembly's registered classes have been
  /// registered for it.
  /// </summary>
  void Start(ICoreAPI api) { }

  /// <summary>
  /// Runs in the owning mod's <c>AssetsLoaded</c>. A module contributing code-first definitions
  /// registers them here: the driver is ordered below the definition system's own injection, so
  /// anything registered in this phase is injected with the rest.
  /// </summary>
  void AssetsLoaded(ICoreAPI api) { }

  /// <summary>
  /// Runs in the owning mod's <c>AssetsFinalize</c>, after the asset-patch pipeline has merged every
  /// mod's JSON. A module loading a catalogue does it here.
  /// </summary>
  void AssetsFinalize(ICoreAPI api) { }
}
