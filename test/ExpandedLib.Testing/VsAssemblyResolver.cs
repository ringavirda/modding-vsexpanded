using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ExpandedLib.Testing;

/// <summary>
/// Resolves the Vintage Story game assemblies (VintagestoryAPI, VSSurvivalMod, the bundled
/// libraries, …) at runtime from the install pointed to by the <c>VINTAGE_STORY</c> environment
/// variable. The test/harness projects reference those DLLs with <c>Private=false</c> (they are
/// never shipped), so without this probe a headless <c>dotnet test</c> run cannot load them.
/// Registered automatically via a module initializer in every assembly that includes this type's
/// <see cref="Register"/> call.
/// </summary>
public static class VsAssemblyResolver
{
  private static readonly object Gate = new();
  private static bool _registered;

  /// <summary>Idempotently hooks <see cref="AppDomain.AssemblyResolve"/> to probe the game folders.</summary>
  public static void Register()
  {
    lock (Gate)
    {
      if (_registered)
        return;
      _registered = true;
    }

    string? vs = Environment.GetEnvironmentVariable("VINTAGE_STORY");
    if (string.IsNullOrEmpty(vs))
      return;

    string[] dirs = [vs, Path.Combine(vs, "Lib"), Path.Combine(vs, "Mods")];

    AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
    {
      string? name = new AssemblyName(args.Name).Name;
      if (name == null)
        return null;
      foreach (string dir in dirs)
      {
        string path = Path.Combine(dir, name + ".dll");
        if (File.Exists(path))
          return Assembly.LoadFrom(path);
      }
      return null;
    };
  }
}

internal static class HarnessModuleInitializer
{
  // Deliberately used in a class library: the resolver must be live before any harness type that
  // references the game assemblies is touched. Safe here - it only hooks AssemblyResolve.
#pragma warning disable CA2255
  [ModuleInitializer]
#pragma warning restore CA2255
  internal static void Init() => VsAssemblyResolver.Register();
}
