using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Registries;

/// <summary>
/// One driver instance's modules: the entry-point instances of <see cref="Mod"/>'s
/// <see cref="ExModuleSet"/>, owned here and nowhere else, run through the same phases and in the
/// same order as the host's own <see cref="ModSystem"/>. Built once per host instance - a rejoined
/// world gets a fresh <see cref="ExModuleHost"/> with fresh instances, never a shared one.
/// </summary>
public sealed class ExModuleHost {
  private readonly Mod _mod;
  private readonly ExModuleSet _set;
  private readonly List<(ExModuleInfo Info, List<IExModule> Instances)> _resolved;

  /// <summary>Builds the host for <paramref name="mod"/> from its discovered module set (see
  /// <see cref="ExModules.For"/>), instantiating every entry point. A constructor that throws is
  /// logged through <paramref name="mod"/>'s logger and that entry point is left out.</summary>
  public ExModuleHost(Mod mod)
    : this(mod, ExModules.For(mod.Info.ModID)) { }

  /// <summary>As <see cref="ExModuleHost(Mod)"/>, against a hand-built <paramref name="set"/> rather
  /// than discovery. For tests.</summary>
  internal ExModuleHost(Mod mod, ExModuleSet set) {
    _mod = mod;
    _set = set;
    _resolved = [.. set.Modules.Select(info => (info, Instantiate(info)))];
  }

  /// <summary>This host's modules, in the dependency order <see cref="ExModules.For"/> gave them.</summary>
  public IReadOnlyList<ExModuleInfo> Modules => _set.Modules;

  /// <summary>Logs <see cref="ExModuleSet.Errors"/>, then runs every module's
  /// <see cref="IExModule.StartPre"/>, module then entry-point order.</summary>
  public void StartPre(ICoreAPI api) {
    foreach (string error in _set.Errors)
      _mod.Logger.Error(error);
    Drive(m => m.StartPre(api));
  }

  /// <summary>Per module, before its entry points: <see cref="ExConfig.LoadAll"/>,
  /// <see cref="EntityRegistry.RegisterAll"/>, and Harmony when the module opted in (see
  /// <see cref="ExModuleInfo.PatchHarmony"/>). Then every module's <see cref="IExModule.Start"/>.</summary>
  public void Start(ICoreAPI api) {
    foreach ((ExModuleInfo info, List<IExModule> instances) in _resolved) {
      ExConfig.LoadAll(api, info.Assembly);
      EntityRegistry.RegisterAll(api, _mod, info.Assembly);
      if (info.PatchHarmony)
        ExHarmony.PatchOnce(info.HarmonyId, info.Assembly);
      foreach (IExModule module in instances)
        Isolate(module, m => m.Start(api));
    }
  }

  /// <summary>Per module, <see cref="CommandRegistry.RegisterAll"/> before its entry points'
  /// <see cref="IExModule.StartServerSide"/>.</summary>
  public void StartServerSide(ICoreServerAPI api) {
    foreach ((ExModuleInfo info, List<IExModule> instances) in _resolved) {
      CommandRegistry.RegisterAll(api, _mod, info.Assembly);
      foreach (IExModule module in instances)
        Isolate(module, m => m.StartServerSide(api));
    }
  }

  /// <summary>Per module, <see cref="PreferenceRegistry.RegisterAll"/> then
  /// <see cref="CommandRegistry.RegisterAll"/> before its entry points'
  /// <see cref="IExModule.StartClientSide"/>.</summary>
  public void StartClientSide(ICoreClientAPI api) {
    foreach ((ExModuleInfo info, List<IExModule> instances) in _resolved) {
      PreferenceRegistry.RegisterAll(api, _mod, info.Assembly);
      CommandRegistry.RegisterAll(api, _mod, info.Assembly);
      foreach (IExModule module in instances)
        Isolate(module, m => m.StartClientSide(api));
    }
  }

  /// <summary>Runs every module's <see cref="IExModule.AssetsLoaded"/>.</summary>
  public void AssetsLoaded(ICoreAPI api) => Drive(m => m.AssetsLoaded(api));

  /// <summary>Runs every module's <see cref="IExModule.AssetsFinalize"/>.</summary>
  public void AssetsFinalize(ICoreAPI api) => Drive(m => m.AssetsFinalize(api));

  /// <summary>Runs every entry point's <see cref="IExModule.Dispose"/>, then unpatches Harmony for
  /// every module that opted in.</summary>
  public void Dispose() {
    Drive(m => m.Dispose());
    foreach ((ExModuleInfo info, _) in _resolved)
      if (info.PatchHarmony)
        ExHarmony.UnpatchAll(info.HarmonyId);
  }

  private List<IExModule> Instantiate(ExModuleInfo info) {
    var instances = new List<IExModule>();
    foreach (Type type in info.EntryPoints) {
      try {
        instances.Add((IExModule)Activator.CreateInstance(type)!);
      } catch (Exception e) {
        _mod.Logger.Error("Module entry point {0} could not be constructed; skipped.", type.FullName);
        _mod.Logger.Error(e);
      }
    }
    return instances;
  }

  private void Drive(Action<IExModule> phase) {
    foreach ((_, List<IExModule> instances) in _resolved)
      foreach (IExModule module in instances)
        Isolate(module, phase);
  }

  private void Isolate(IExModule module, Action<IExModule> phase) {
    try {
      phase(module);
    } catch (Exception e) {
      _mod.Logger.Error(
        "Module {0} threw; the rest of the host continues without what it does.",
        module.GetType().FullName
      );
      _mod.Logger.Error(e);
    }
  }
}
