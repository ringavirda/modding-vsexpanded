using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
#if GAME_GE_1_22
using Vintagestory.Common.Datastructures;
#endif

namespace ExpandedLib.Testing;

/// <summary>
/// Real-asset loading for <see cref="TestWorld"/>: drives the game's own <c>AssetManager</c> and
/// <c>ModRegistryObjectTypeLoader</c> against a mod's actual JSON/code, so a test gets real, resolved
/// <see cref="Block"/>/<see cref="Item"/> instances rather than <see cref="TestWorld.RegisterItem"/>'s
/// hand-built stand-ins. See <c>docs/internal/research/2026-09-06-asset-loading-spike.md</c> for the
/// wall-by-wall trace this was built from.
/// </summary>
public sealed partial class TestWorld {
  /// <summary>
  /// Loads one mod's real assets through the game's own asset manager and object loader - the same
  /// pipeline a dedicated server runs at startup - and registers every resulting
  /// <see cref="Block"/>/<see cref="Item"/> into this <see cref="TestWorld"/>
  /// (<see cref="Register(Block)"/>/<see cref="Register(Item)"/>). Runs in an isolated substituted
  /// <c>ICoreServerAPI</c>, not <see cref="Api"/> - see the design note this file's type doc cites.
  /// Scope: base <c>game</c> domain assets plus <paramref name="modPath"/>'s own, not vanilla
  /// survival/creative content (their blocks need classes only <c>VSSurvivalMod</c> registers).
  /// </summary>
  /// <param name="modPath">A mod's folder: <c>modinfo.json</c> at its root, assets under
  /// <c>assets/&lt;modid&gt;/</c>, and its compiled dll somewhere under <c>bin/</c>.</param>
  /// <param name="gamePath">The game install to read base assets from; defaults to
  /// <see cref="VsAssemblyResolver.InstallPath"/>.</param>
  /// <exception cref="InvalidOperationException">No game install resolves, <paramref name="modPath"/>
  /// has no <c>modinfo.json</c>, or its compiled dll cannot be found under <c>bin/</c>.</exception>
  public TestWorld LoadAssets(string modPath, string? gamePath = null) {
    gamePath ??=
      VsAssemblyResolver.InstallPath
      ?? throw new InvalidOperationException(
        "No game install found - set the game's env var or provision .game/<slug>."
      );
    string assetsPath = Path.Combine(gamePath, "assets");

    string modInfoPath = Path.Combine(modPath, "modinfo.json");
    if (!File.Exists(modInfoPath))
      throw new InvalidOperationException(
        $"No modinfo.json under '{modPath}'."
      );
    var modInfoJson = JObject.Parse(File.ReadAllText(modInfoPath));
    string modId =
      (string?)modInfoJson["modid"]
      ?? throw new InvalidOperationException($"'{modInfoPath}' has no modid.");
    string version = (string?)modInfoJson["version"] ?? "0.0.0";

    Assembly modAssembly = Assembly.LoadFrom(FindModAssembly(modPath, modId));

    // The base game domain only - see the type doc for why survival/creative are excluded.
    var mgr = new AssetManager(assetsPath, EnumAppSide.Server);
    mgr.InitAndLoadBaseAssets(Log);
    MirrorAssets(mgr, Path.Combine(modPath, "assets", modId), modId);

    // GamePaths.AssetsPath/Lang.Load are process-wide statics the object loader's Lang.Get() calls
    // need primed; harmless to set repeatedly across LoadAssets calls in the same process.
    ReflectionHelpers.SetStaticField(
      typeof(GamePaths),
      "<AssetsPath>k__BackingField",
      assetsPath
    );
    Lang.Load(Log, mgr, "en");

    ICoreServerAPI loaderApi = BuildLoaderApi(
      mgr,
      out ClassRegistry rawClassRegistry
    );

    Mods.Add(modId, version);
    Mod mod = Mods.GetMod(modId)!;

    foreach (
      Type t in modAssembly
        .GetTypes()
        .Where(t => typeof(ModSystem).IsAssignableFrom(t) && !t.IsAbstract)
    ) {
      var sys = (ModSystem)Activator.CreateInstance(t)!;
      ReflectionHelpers.SetField(sys, "<Mod>k__BackingField", mod);
      if (sys.ShouldLoad(EnumAppSide.Server))
        sys.Start(loaderApi);
    }
    // Runs regardless of whether the mod itself is code-first: a mod that ships plain JSON registers
    // no definitions here and this is a no-op, matching production (exlib's own ModSystem always
    // runs, at ExecuteOrder 0.04, whether or not anyone used the code-first API).
    new ExDefinitionModSystem().AssetsLoaded(loaderApi);

    RunObjectLoader(loaderApi);

    foreach (
      Block block in loaderApi
        .ReceivedCalls()
        .Where(c =>
          c.GetMethodInfo().Name == nameof(ICoreServerAPI.RegisterBlock)
        )
        .Select(c => (Block)c.GetArguments()[0]!)
    )
      Register(block);
    foreach (
      Item item in loaderApi
        .ReceivedCalls()
        .Where(c =>
          c.GetMethodInfo().Name == nameof(ICoreServerAPI.RegisterItem)
        )
        .Select(c => (Item)c.GetArguments()[0]!)
    )
      Register(item);

    return this;
  }

  /// <summary>Copies every asset under <paramref name="fullPath"/> for <paramref name="domain"/>
  /// into <paramref name="mgr"/>'s live asset dictionary. <c>AssetManager.AddPathOrigin</c> alone
  /// only appends the origin to a list the engine's object loader never re-scans (its <c>GetMany</c>
  /// reads the already-populated dictionary) - assets must be added directly.</summary>
  private static void MirrorAssets(
    AssetManager mgr,
    string fullPath,
    string domain
  ) {
    if (!Directory.Exists(fullPath))
      return;
    var origin = new PathOrigin(domain, fullPath);
    foreach (AssetCategory cat in KnownCategories)
      foreach (IAsset a in origin.GetAssets(cat, shouldLoad: true))
        mgr.Add(a.Location, a);
  }

  private static readonly AssetCategory[] KnownCategories =
  [
    AssetCategory.blocktypes,
    AssetCategory.itemtypes,
    AssetCategory.entities,
    AssetCategory.recipes,
    AssetCategory.worldproperties,
    AssetCategory.patches,
    AssetCategory.lang,
    AssetCategory.config,
  ];

  /// <summary>Finds the mod's compiled assembly under <c>modPath/bin/</c>: the first dll (recursive
  /// search) whose file name matches <paramref name="modId"/> case-insensitively, matching the
  /// <c>&lt;AssemblyName&gt;</c> every mod project in this repo sets to its modid.</summary>
  private static string FindModAssembly(string modPath, string modId) {
    string binPath = Path.Combine(modPath, "bin");
    if (!Directory.Exists(binPath))
      throw new InvalidOperationException(
        $"No compiled assembly under '{binPath}' - build '{modPath}' first."
      );
    return Directory
        .EnumerateFiles(binPath, "*.dll", SearchOption.AllDirectories)
        .FirstOrDefault(p =>
          string.Equals(
            Path.GetFileNameWithoutExtension(p),
            modId,
            StringComparison.OrdinalIgnoreCase
          )
        )
      ?? throw new InvalidOperationException(
        $"No '{modId}.dll' under '{binPath}' - build '{modPath}' first."
      );
  }

  /// <summary>
  /// Builds the isolated <c>ICoreServerAPI</c> substitute the object loader runs against, wiring
  /// only what a headless replay of its own <c>AssetsLoaded</c> needs (found by the asset-loading
  /// spike, see the type doc): a real <c>ClassRegistry</c> reached both through <c>ClassRegistry</c>
  /// itself and through the top-level <c>RegisterBlockClass</c>-family members (which forward to
  /// <c>ServerMain</c>'s own registry in production, not through <c>api.ClassRegistry</c>), real tag
  /// registries (Castle's dynamic proxy cannot intercept their <c>ReadOnlySpan&lt;string&gt;</c>
  /// parameters - <see cref="System.InvalidProgramException"/> - so a substitute cannot stand in),
  /// and a real logger reachable through both <c>Logger</c> and <c>Server.Logger</c> (the loader
  /// logs some errors through the latter).
  /// </summary>
  private ICoreServerAPI BuildLoaderApi(
    AssetManager mgr,
    out ClassRegistry rawClassRegistry
  ) {
    var api = Substitute.For<ICoreServerAPI>();
    var coreApi = (ICoreAPI)api;
    coreApi.Assets.Returns(mgr);
    coreApi.Logger.Returns(Log);
    coreApi.Side.Returns(EnumAppSide.Server);
    coreApi.World.Returns(World);
    coreApi.ModLoader.Returns(Mods);

    rawClassRegistry = new ClassRegistry();
    ClassRegistry captured = rawClassRegistry;
    coreApi.ClassRegistry.Returns(new ClassRegistryAPI(World, captured));
    coreApi
      .When(x => x.RegisterBlockClass(Arg.Any<string>(), Arg.Any<Type>()))
      .Do(ci =>
        captured.RegisterBlockClass(ci.ArgAt<string>(0), ci.ArgAt<Type>(1))
      );
    coreApi
      .When(x => x.RegisterBlockEntityClass(Arg.Any<string>(), Arg.Any<Type>()))
      .Do(ci =>
        captured.RegisterBlockEntityType(ci.ArgAt<string>(0), ci.ArgAt<Type>(1))
      );
    coreApi
      .When(x => x.RegisterItemClass(Arg.Any<string>(), Arg.Any<Type>()))
      .Do(ci =>
        captured.RegisterItemClass(ci.ArgAt<string>(0), ci.ArgAt<Type>(1))
      );
    coreApi
      .When(x =>
        x.RegisterBlockBehaviorClass(Arg.Any<string>(), Arg.Any<Type>())
      )
      .Do(ci =>
        captured.RegisterBlockBehaviorClass(
          ci.ArgAt<string>(0),
          ci.ArgAt<Type>(1)
        )
      );
    coreApi
      .When(x =>
        x.RegisterBlockEntityBehaviorClass(Arg.Any<string>(), Arg.Any<Type>())
      )
      .Do(ci =>
        captured.RegisterBlockEntityBehaviorClass(
          ci.ArgAt<string>(0),
          ci.ArgAt<Type>(1)
        )
      );

    // ICoreAPI.CollectibleTagRegistry/EntityTagRegistry (and the loader's PreloadTags that needs
    // them) do not exist before 1.22.
#if GAME_GE_1_22
    coreApi.CollectibleTagRegistry.Returns(
      new ConcurrentTagRegistry(Log, "collectible")
    );
    coreApi.EntityTagRegistry.Returns(
      new ConcurrentTagRegistryFast(Log, "entity")
    );
#endif

    var serverApi = Substitute.For<IServerAPI>();
    serverApi.Logger.Returns(Log);
    api.Server.Returns(serverApi);

    return api;
  }

  /// <summary>Reflectively runs <c>ModRegistryObjectTypeLoader.AssetsLoaded</c> - a public
  /// <c>Vintagestory.ServerMods.NoObf</c> class, but a game-version-fragile one to hard-reference, so
  /// it is found by name each call rather than referenced statically.</summary>
  private static void RunObjectLoader(ICoreServerAPI api) {
    Type loaderType =
      Assembly
        .Load("VSEssentials")
        .GetType("Vintagestory.ServerMods.NoObf.ModRegistryObjectTypeLoader")
      ?? throw new InvalidOperationException(
        "VSEssentials no longer exposes Vintagestory.ServerMods.NoObf.ModRegistryObjectTypeLoader."
      );
    object loader =
      Activator.CreateInstance(loaderType, nonPublic: true)
      ?? throw new InvalidOperationException(
        $"Could not construct {loaderType.FullName}."
      );
    try {
      loaderType.GetMethod("AssetsLoaded")!.Invoke(loader, [api]);
    } catch (TargetInvocationException e) when (e.InnerException != null) {
      throw e.InnerException;
    }
  }
}
