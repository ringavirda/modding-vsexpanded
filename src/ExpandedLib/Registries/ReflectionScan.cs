using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Shared reflection helper for the attribute-driven registries
/// (<see cref="Entities.EntityRegistry"/>, <see cref="Commands.CommandRegistry"/>,
/// <see cref="Preferences.PreferenceRegistry"/>).
/// </summary>
public static class ReflectionScan {
  /// <summary>
  /// Returns every concrete (non-abstract) class in <paramref name="asm"/>, tolerating a
  /// partial load (<see cref="ReflectionTypeLoadException"/>) so one unloadable type can't
  /// break registration of the rest.
  /// </summary>
  public static Type[] GetCandidateTypes(Assembly asm) {
    try {
      return asm.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false })
        .ToArray();
    } catch (ReflectionTypeLoadException ex) {
      return ex
        .Types.Where(t => t is { IsClass: true, IsAbstract: false })
        .ToArray()!;
    }
  }

  /// <summary>
  /// Validates that <paramref name="type"/> is assignable to <typeparamref name="T"/> and, if so,
  /// activates it through its parameterless constructor. A mismatch (a mis-applied register
  /// attribute) logs a warning and returns <c>false</c> so the caller skips the type rather than
  /// throwing. Shared by the activating registries: commands, sub-commands and preferences.
  /// </summary>
  public static bool TryActivate<T>(
    ICoreAPI api,
    string modId,
    Type type,
    out T instance
  )
    where T : class {
    if (!typeof(T).IsAssignableFrom(type)) {
      api.Logger.Warning(
        "[{0}] {1} is marked for registration but does not implement {2}; skipped.",
        modId,
        type.FullName,
        typeof(T).Name
      );
      instance = null!;
      return false;
    }

    instance = (T)Activator.CreateInstance(type)!;
    return true;
  }
}
