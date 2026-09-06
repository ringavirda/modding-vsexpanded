using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// The Harmony bootstrap every reference mod copied by hand: patch an assembly's uncategorised
/// <c>[HarmonyPatch]</c> classes once per process, apply a named category only when a required mod is
/// present, and unpatch everything cleanly on <see cref="ModSystem.Dispose"/>.
/// </summary>
public static class ExHarmony {
  // Harmony's own PatchCategory is not idempotent: calling it twice with the same owner id and
  // category stacks a second copy of every prefix/postfix/etc (unlike PatchAllUncategorized, which
  // Harmony.HasAnyPatches already guards). Tracked here as "<harmony id>::<category>" so a mod that
  // calls PatchCategoryWhenLoaded from Start every world load, as ModSystem.Start does, does not grow
  // duplicate patches; cleared on UnpatchAll so a later re-patch after unpatching applies again.
  private static readonly HashSet<string> AppliedCategories = [];

  /// <summary>
  /// Applies every uncategorised <c>[HarmonyPatch]</c> class in <paramref name="assembly"/> under
  /// <paramref name="mod"/>'s id, once per process however many times this is called (guarded by
  /// <see cref="Harmony.HasAnyPatches(string)"/>). A class carrying <c>[HarmonyPatchCategory]</c> is
  /// left unpatched; apply it explicitly through <see cref="PatchCategoryWhenLoaded"/>. Returns the
  /// <see cref="Harmony"/> instance, to pass to <see cref="UnpatchAll"/> in <c>Dispose</c>.
  /// </summary>
  public static Harmony PatchOnce(Mod mod, Assembly assembly) {
    var harmony = new Harmony(mod.Info.ModID);
    if (!Harmony.HasAnyPatches(mod.Info.ModID))
      harmony.PatchAllUncategorized(assembly);
    return harmony;
  }

  /// <summary>
  /// Applies the <c>[HarmonyPatchCategory(category)]</c> classes in <paramref name="assembly"/> when
  /// <paramref name="requiredModId"/> is loaded on <paramref name="api"/>'s side (see
  /// <see cref="ExMods.IsLoaded"/>); does nothing and returns false otherwise. Safe to call more than
  /// once for the same <paramref name="harmony"/> and <paramref name="category"/>: a repeat is a no-op
  /// until <see cref="UnpatchAll"/> runs.
  /// </summary>
  public static bool PatchCategoryWhenLoaded(
    ICoreAPI api,
    Harmony harmony,
    Assembly assembly,
    string category,
    string requiredModId
  ) {
    if (!ExMods.IsLoaded(api, requiredModId))
      return false;
    if (AppliedCategories.Add(harmony.Id + "::" + category))
      harmony.PatchCategory(assembly, category);
    return true;
  }

  /// <summary>Unpatches everything registered under <paramref name="mod"/>'s id. Safe to call twice
  /// (Harmony's own unpatch is a no-op with nothing left to remove).</summary>
  public static void UnpatchAll(Mod mod) {
    string id = mod.Info.ModID;
    new Harmony(id).UnpatchAll(id);
    AppliedCategories.RemoveWhere(key => key.StartsWith(id + "::"));
  }
}
