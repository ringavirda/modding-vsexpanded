using System.Reflection;
using ExpandedLib.Config;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Registries;

/// <summary>
/// The zero-line registration rung: a mod deriving this gets its config, its attribute-marked
/// classes, its commands and its preferences registered with no explicit calls at all. Each
/// lifecycle hook below runs the registries for that phase, then an empty overridable hook of the
/// same name, so a mod that needs nothing more than the default order writes an empty class body.
/// A mod that needs a different order (a preference read before a command it names, say) keeps the
/// explicit calls documented on <see cref="ExpandedLib.Registries"/>'s own pages instead of deriving
/// this base.
/// </summary>
public abstract class ExModSystem : ModSystem {
  /// <summary>The assembly scanned by every registry call below. Defaults to the type's own
  /// assembly; override only for a mod split across assemblies.</summary>
  protected virtual Assembly Assembly => GetType().Assembly;

  /// <summary>When true, <see cref="Start"/> patches this assembly's uncategorised
  /// <c>[HarmonyPatch]</c> classes through <see cref="ExHarmony.PatchOnce"/>, and
  /// <see cref="Dispose"/> unpatches them. Off by default: a mod with no Harmony patches pays
  /// nothing for the check.</summary>
  protected virtual bool PatchHarmony => false;

  // Lazy so a phase called on its own (tests do this) still works without StartPre having run
  // first, and per instance so a rejoined world's ExModSystem drives fresh module instances rather
  // than a previous world's.
  private ExModuleHost? _modules;

  /// <summary>This mod's own modules (see <see cref="ExModules.For"/>), built against whichever
  /// phase's <paramref name="api"/> runs first.</summary>
  private ExModuleHost Modules(ICoreAPI api) =>
    _modules ??= new ExModuleHost(Mod, api);

  /// <summary>Runs every own module's <see cref="IExModule.StartPre"/>, then calls
  /// <see cref="OnStartPre"/>. A mod with no modules of its own only gets the hook.</summary>
  public override void StartPre(ICoreAPI api) {
    Modules(api).StartPre(api);
    OnStartPre(api);
  }

  /// <summary>Loads every <c>[ExConfigRegister]</c> accessor in <see cref="Assembly"/> through
  /// <see cref="ExConfig.LoadAll"/>, then registers every <c>[BlockRegister]</c>/etc class and
  /// code-first definition through <see cref="EntityRegistry.RegisterAll"/>, then registers and
  /// starts every own module (see <see cref="IExModule"/>), then calls <see cref="OnStart"/>. Also
  /// patches Harmony when <see cref="PatchHarmony"/> is true.</summary>
  public override void Start(ICoreAPI api) {
    ExConfig.LoadAll(api, Assembly);
    EntityRegistry.RegisterAll(api, Mod, Assembly);
    if (PatchHarmony)
      ExHarmony.PatchOnce(Mod, Assembly);
    Modules(api).Start(api);
    OnStart(api);
  }

  /// <summary>Runs every own module's <see cref="IExModule.AssetsLoaded"/>, then calls
  /// <see cref="OnAssetsLoaded"/>.</summary>
  public override void AssetsLoaded(ICoreAPI api) {
    Modules(api).AssetsLoaded(api);
    OnAssetsLoaded(api);
  }

  /// <summary>Registers every <c>[CommandRegister]</c>/<c>[SubCommandRegister]</c> class in
  /// <see cref="Assembly"/> on the server, then registers and runs every own module's server
  /// commands and <see cref="IExModule.StartServerSide"/>, then calls
  /// <see cref="OnStartServerSide"/>.</summary>
  public override void StartServerSide(ICoreServerAPI api) {
    CommandRegistry.RegisterAll(api, Mod, Assembly);
    Modules(api).StartServerSide(api);
    OnStartServerSide(api);
  }

  /// <summary>Registers every <c>[PreferenceRegister]</c> class, then every
  /// <c>[CommandRegister]</c>/<c>[SubCommandRegister]</c> class in <see cref="Assembly"/> on the
  /// client - preferences first, so a command naming one finds it already registered - then
  /// registers and runs every own module's client preferences, commands and
  /// <see cref="IExModule.StartClientSide"/>, then calls <see cref="OnStartClientSide"/>.</summary>
  public override void StartClientSide(ICoreClientAPI api) {
    PreferenceRegistry.RegisterAll(api, Mod, Assembly);
    CommandRegistry.RegisterAll(api, Mod, Assembly);
    Modules(api).StartClientSide(api);
    OnStartClientSide(api);
  }

  /// <summary>Runs every own module's <see cref="IExModule.AssetsFinalize"/>, then calls
  /// <see cref="OnAssetsFinalize"/>. Registration happens earlier
  /// (<see cref="Start"/>/<see cref="StartServerSide"/>/<see cref="StartClientSide"/>); this hook is
  /// for validation against the now-final catalogues.</summary>
  public override void AssetsFinalize(ICoreAPI api) {
    Modules(api).AssetsFinalize(api);
    OnAssetsFinalize(api);
  }

  /// <summary>Disposes this mod's modules, unpatches this assembly's Harmony instance when
  /// <see cref="PatchHarmony"/> patched it in <see cref="Start"/>, and clears the module host so a
  /// rejoined world builds a fresh one.</summary>
  public override void Dispose() {
    _modules?.Dispose();
    if (PatchHarmony)
      ExHarmony.UnpatchAll(Mod);
    _modules = null;
    base.Dispose();
  }

  /// <summary>Runs before any registration, in <see cref="StartPre"/>. Empty by default.</summary>
  protected virtual void OnStartPre(ICoreAPI api) { }

  /// <summary>Runs in <see cref="AssetsLoaded"/>, after every companion module's own. Empty by
  /// default.</summary>
  protected virtual void OnAssetsLoaded(ICoreAPI api) { }

  /// <summary>Runs after config and entity registration in <see cref="Start"/>. Empty by default.</summary>
  protected virtual void OnStart(ICoreAPI api) { }

  /// <summary>Runs after command registration in <see cref="StartServerSide"/>. Empty by default.</summary>
  protected virtual void OnStartServerSide(ICoreServerAPI api) { }

  /// <summary>Runs after preference and command registration in <see cref="StartClientSide"/>. Empty
  /// by default.</summary>
  protected virtual void OnStartClientSide(ICoreClientAPI api) { }

  /// <summary>Runs in <see cref="AssetsFinalize"/>. Empty by default.</summary>
  protected virtual void OnAssetsFinalize(ICoreAPI api) { }
}
