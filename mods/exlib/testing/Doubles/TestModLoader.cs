using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using Vintagestory.API.Common;

namespace ExpandedLib.Testing;

/// <summary>
/// A real (non-substitute) <see cref="IModLoader"/>: <see cref="Add"/> registers an enabled mod by
/// id and version, <see cref="Register"/> registers a live <see cref="ModSystem"/> so
/// <c>GetModSystem&lt;T&gt;</c> resolves it. Answers <see cref="IsModEnabled"/> and the three
/// reflection-probed aliases (<see cref="IsModLoaded"/>, <see cref="HasMod"/>,
/// <see cref="HasModId"/>) some mods call instead of the interface member, on the theory that it
/// might not exist on an older API - all four agree here.
/// </summary>
public sealed class TestModLoader : IModLoader {
  private readonly Dictionary<string, Mod> _mods = new();
  private readonly List<ModSystem> _systems = [];

  /// <summary>Registers an enabled mod. Disabled (<paramref name="enabled"/> false) mods are not
  /// added at all, matching production: <c>Mods</c>/<c>GetMod</c> only ever see enabled ones.
  /// <paramref name="dependencies"/> are the mod IDs it declares in <c>modinfo.json</c>, as
  /// <see cref="ModInfo.Dependencies"/> reports them.</summary>
  public TestModLoader Add(
    string modId,
    string version,
    bool enabled = true,
    params string[] dependencies
  ) {
    if (!enabled)
      return this;
    Mod mod = Substitute.For<Mod>();
    ReflectionHelpers.SetProperty(
      mod,
      nameof(Mod.Info),
      new ModInfo {
        ModID = modId,
        Version = version,
        Dependencies = dependencies.Select(d => new ModDependency(d)).ToList(),
      }
    );
    _mods[modId] = mod;
    return this;
  }

  /// <summary>Registers a live mod system so <c>GetModSystem</c>/<c>GetModSystem&lt;T&gt;</c>
  /// resolve it, as <see cref="TestWorld"/> does for its own
  /// <see cref="ExpandedLib.Networks.BlockNetworkModSystem"/>.</summary>
  public TestModLoader Register(ModSystem system) {
    _systems.Add(system);
    return this;
  }

  public IEnumerable<Mod> Mods => _mods.Values;
  public IEnumerable<ModSystem> Systems => _systems;

  public Mod? GetMod(string modID) => _mods.GetValueOrDefault(modID);

  public bool IsModEnabled(string modID) => _mods.ContainsKey(modID);

  public ModSystem? GetModSystem(string fullName) =>
    _systems.FirstOrDefault(s => s.GetType().FullName == fullName);

  public T? GetModSystem<T>(bool withInheritance = true)
    where T : ModSystem =>
    (T?)(
      withInheritance
        ? _systems.FirstOrDefault(s => s is T)
        : _systems.FirstOrDefault(s => s.GetType() == typeof(T))
    );

  public bool IsModSystemEnabled(string fullName) => GetModSystem(fullName) != null;

  /// <summary>Alias for <see cref="IsModEnabled"/>; not part of <see cref="IModLoader"/>, but some
  /// mods reflect for it instead, guessing the member name might differ across API versions.</summary>
  public bool IsModLoaded(string modId) => IsModEnabled(modId);

  /// <summary>As <see cref="IsModLoaded"/>.</summary>
  public bool HasMod(string modId) => IsModEnabled(modId);

  /// <summary>As <see cref="IsModLoaded"/>.</summary>
  public bool HasModId(string modId) => IsModEnabled(modId);
}
