using System.Reflection;
using ExpandedLib.Registries;
using HarmonyLib;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shared Harmony bootstrap (<see cref="ExHarmony"/>). Patches a test-only class on a test-only
/// method - never a game type - under unique mod ids per case so cases cannot leave patches behind
/// for one another; each unpatches in a <c>finally</c>.
/// </summary>
[Collection(ExHarmonyCollection.Name)]
public class ExHarmonyTests {
  private const string TestCategory = "exlibtest.exharmony-category";

  private static class UncategorizedTarget {
    public static void Method() { }
  }

  [HarmonyPatch(
    typeof(UncategorizedTarget),
    nameof(UncategorizedTarget.Method)
  )]
  private static class UncategorizedPatch {
    private static void Prefix() { }
  }

  private static class CategorizedTarget {
    public static void Method() { }
  }

  [HarmonyPatchCategory(TestCategory)]
  [HarmonyPatch(typeof(CategorizedTarget), nameof(CategorizedTarget.Method))]
  private static class CategorizedPatch {
    private static void Prefix() { }
  }

  private static Mod FakeMod(string modId) {
    var mod = Substitute.For<Mod>();
    typeof(Mod)
      .GetProperty("Info")!
      .SetValue(mod, new ModInfo { ModID = modId });
    return mod;
  }

  private static ICoreAPI FakeApi(params string[] loadedModIds) {
    var api = Substitute.For<ICoreAPI>();
    var loader = Substitute.For<IModLoader>();
    loader
      .IsModEnabled(Arg.Any<string>())
      .Returns(call =>
        System.Array.IndexOf(loadedModIds, (string)call[0]) >= 0
      );
    api.ModLoader.Returns(loader);
    return api;
  }

  [Fact]
  public void PatchOnce_applied_twice_yields_one_patch() {
    var mod = FakeMod("exlibtest.exharmony-once");
    MethodBase original = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    try {
      ExHarmony.PatchOnce(mod, typeof(ExHarmonyTests).Assembly);
      ExHarmony.PatchOnce(mod, typeof(ExHarmonyTests).Assembly);

      Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void PatchOnce_is_once_per_assembly_and_id() {
    const string id = "exlibtest.exharmony-once-string-id";
    MethodBase original = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    try {
      ExHarmony.PatchOnce(id, typeof(ExHarmonyTests).Assembly);
      ExHarmony.PatchOnce(id, typeof(ExHarmonyTests).Assembly);

      Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);
    } finally {
      ExHarmony.UnpatchAll(id);
    }
  }

  [Fact]
  public void An_earlier_category_patch_does_not_suppress_the_uncategorised_ones() {
    var mod = FakeMod("exlibtest.exharmony-category-then-uncategorized");
    var harmony = new Harmony(mod.Info.ModID);
    var api = FakeApi("required-mod");
    MethodBase categorized = typeof(CategorizedTarget).GetMethod(
      nameof(CategorizedTarget.Method)
    )!;
    MethodBase uncategorized = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    try {
      ExHarmony.PatchCategoryWhenLoaded(
        api,
        harmony,
        typeof(ExHarmonyTests).Assembly,
        TestCategory,
        "required-mod"
      );
      ExHarmony.PatchOnce(mod, typeof(ExHarmonyTests).Assembly);

      Assert.Equal(1, Harmony.GetPatchInfo(categorized)?.Prefixes.Count);
      Assert.Equal(1, Harmony.GetPatchInfo(uncategorized)?.Prefixes.Count);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void PatchCategoryWhenLoaded_false_and_unpatched_when_the_mod_is_absent() {
    var mod = FakeMod("exlibtest.exharmony-category-absent");
    var harmony = new Harmony(mod.Info.ModID);
    var api = FakeApi(); // no mods loaded
    MethodBase original = typeof(CategorizedTarget).GetMethod(
      nameof(CategorizedTarget.Method)
    )!;

    try {
      bool applied = ExHarmony.PatchCategoryWhenLoaded(
        api,
        harmony,
        typeof(ExHarmonyTests).Assembly,
        TestCategory,
        "required-mod"
      );

      Assert.False(applied);
      var info = Harmony.GetPatchInfo(original);
      Assert.True(info == null || info.Prefixes.Count == 0);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void PatchCategoryWhenLoaded_true_and_patches_when_the_mod_is_present() {
    var mod = FakeMod("exlibtest.exharmony-category-present");
    var harmony = new Harmony(mod.Info.ModID);
    var api = FakeApi("required-mod");
    MethodBase original = typeof(CategorizedTarget).GetMethod(
      nameof(CategorizedTarget.Method)
    )!;

    try {
      bool applied = ExHarmony.PatchCategoryWhenLoaded(
        api,
        harmony,
        typeof(ExHarmonyTests).Assembly,
        TestCategory,
        "required-mod"
      );

      Assert.True(applied);
      Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void PatchCategoryWhenLoaded_does_not_stack_on_a_repeat_call() {
    var mod = FakeMod("exlibtest.exharmony-category-repeat");
    var harmony = new Harmony(mod.Info.ModID);
    var api = FakeApi("required-mod");
    MethodBase original = typeof(CategorizedTarget).GetMethod(
      nameof(CategorizedTarget.Method)
    )!;

    try {
      ExHarmony.PatchCategoryWhenLoaded(
        api,
        harmony,
        typeof(ExHarmonyTests).Assembly,
        TestCategory,
        "required-mod"
      );
      ExHarmony.PatchCategoryWhenLoaded(
        api,
        harmony,
        typeof(ExHarmonyTests).Assembly,
        TestCategory,
        "required-mod"
      );

      Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);
    } finally {
      ExHarmony.UnpatchAll(mod);
    }
  }

  [Fact]
  public void UnpatchAll_removes_and_is_safe_twice() {
    var mod = FakeMod("exlibtest.exharmony-unpatch");
    MethodBase original = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;
    ExHarmony.PatchOnce(mod, typeof(ExHarmonyTests).Assembly);

    ExHarmony.UnpatchAll(mod);
    ExHarmony.UnpatchAll(mod);

    var info = Harmony.GetPatchInfo(original);
    Assert.True(info == null || info.Prefixes.Count == 0);
  }
}
