using System.Reflection;
using ExpandedLib.Testing;
using HarmonyLib;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="HarmonyFixture"/>: patches and reverts a test-only target and the vanilla server-safe
/// <see cref="CollectibleObject.GetHeldItemName"/>, a second fixture for the same mod id does not
/// double-patch, and the category form applies only its categorised class. Joins
/// <see cref="ExHarmonyCollection"/> since Harmony patches are process-wide.
/// </summary>
[Collection(ExHarmonyCollection.Name)]
public class HarmonyFixtureTests {
  private static class UncategorizedTarget {
    public static void Method() { }
  }

  [HarmonyPatch(typeof(UncategorizedTarget), nameof(UncategorizedTarget.Method))]
  private static class UncategorizedPatch {
    private static void Prefix() { }
  }

  [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemName))]
  private static class VanillaPatch {
    private static bool Prefix() => true;
  }

  private const string TestCategory = "exlibtest.harmonyfixture-category";

  [HarmonyPatchCategory(TestCategory)]
  [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemName))]
  private static class CategorizedPatch {
    private static bool Prefix() => true;
  }

  [Fact]
  public void Patches_a_test_only_target_and_reverts_on_dispose() {
    MethodBase original = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    using (
      var fixture = new HarmonyFixture(
        "exlibtest.harmonyfixture-testonly",
        typeof(HarmonyFixtureTests).Assembly
      )
    ) {
      Assert.True(fixture.IsPatched(original));
      Assert.Contains(original, fixture.PatchedMethods);
    }

    var info = Harmony.GetPatchInfo(original);
    Assert.True(info == null || info.Prefixes.Count == 0);
  }

  [Fact]
  public void Patches_the_vanilla_server_safe_target_and_reverts_on_dispose() {
    MethodBase original = typeof(CollectibleObject).GetMethod(
      nameof(CollectibleObject.GetHeldItemName)
    )!;

    using (
      var fixture = new HarmonyFixture(
        "exlibtest.harmonyfixture-vanilla",
        typeof(HarmonyFixtureTests).Assembly
      )
    ) {
      Assert.True(fixture.IsPatched(original));
    }

    var info = Harmony.GetPatchInfo(original);
    Assert.True(info == null || info.Prefixes.Count == 0);
  }

  [Fact]
  public void A_second_fixture_with_the_same_id_does_not_double_patch() {
    const string modId = "exlibtest.harmonyfixture-repeat";
    MethodBase original = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    using var first = new HarmonyFixture(modId, typeof(HarmonyFixtureTests).Assembly);
    using var second = new HarmonyFixture(modId, typeof(HarmonyFixtureTests).Assembly);

    Assert.Equal(1, Harmony.GetPatchInfo(original)?.Prefixes.Count);
  }

  [Fact]
  public void The_category_form_applies_only_the_categorised_class() {
    MethodBase categorized = typeof(CollectibleObject).GetMethod(
      nameof(CollectibleObject.GetHeldItemName)
    )!;
    MethodBase uncategorized = typeof(UncategorizedTarget).GetMethod(
      nameof(UncategorizedTarget.Method)
    )!;

    using var fixture = new HarmonyFixture(
      "exlibtest.harmonyfixture-category-only",
      typeof(HarmonyFixtureTests).Assembly,
      TestCategory
    );

    Assert.True(fixture.IsPatched(categorized));
    Assert.False(fixture.IsPatched(uncategorized));
  }
}
