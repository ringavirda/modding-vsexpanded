using System.Reflection;
using ExpandedLib.Definitions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The known-key set the warning in <see cref="ExDefinitionModSystem"/> checks a def's root keys
/// against, and the <c>Raw</c>/<c>RawByType</c> forwarders <see cref="ExBlockDef.RootKey(string, object)"/>
/// replaced.
/// </summary>
public class KnownRootKeysTests {
  [Fact]
  public void Block_contains_shape_and_variantgroups() {
    Assert.Contains("shape", KnownRootKeys.Block);
    Assert.Contains("variantgroups", KnownRootKeys.Block);
  }

  [Fact]
  public void Item_contains_attributes_and_creativeinventory() {
    Assert.Contains("attributes", KnownRootKeys.Item);
    Assert.Contains("creativeinventory", KnownRootKeys.Item);
  }

  [Fact]
  public void Block_does_not_contain_a_made_up_key() {
    Assert.False(KnownRootKeys.IsKnownBlockKey("thicknes"));
  }

  [Fact]
  public void A_def_with_an_unknown_root_key_is_reported() {
    ExBlockDef def = ExBlockDef.Create("d", "c").RootKey("thicknes", 3);
    Assert.Contains("thicknes", ExDefinitionModSystem.Audit(def));

    ExItemDef itemDef = ExItemDef.Create("d", "c").RootKey("thicknes", 3);
    Assert.Contains("thicknes", ExDefinitionModSystem.Audit(itemDef));
  }

  [Fact]
  public void A_def_with_only_known_root_keys_is_not_reported() {
    ExBlockDef def = ExBlockDef.Create("d", "c").Shape("d:block/basic/cube");
    Assert.Empty(ExDefinitionModSystem.Audit(def));
  }

  [Theory]
  [InlineData(typeof(ExBlockDef), nameof(ExBlockDef.RawByType))]
  [InlineData(typeof(ExBlockDef), nameof(ExBlockDef.Raw))]
  [InlineData(typeof(ExItemDef), nameof(ExItemDef.Raw))]
  public void Raw_forwards_to_RootKey_and_is_obsolete(
    System.Type declaringType,
    string methodName
  ) {
    bool anyObsolete = false;
    foreach (MethodInfo method in declaringType.GetMethods(
      BindingFlags.Public | BindingFlags.Instance
    ))
      if (method.Name == methodName && method.GetCustomAttribute<System.ObsoleteAttribute>() is { } obsolete) {
        anyObsolete = true;
        Assert.Contains("RootKey", obsolete.Message);
      }

    Assert.True(anyObsolete, $"{declaringType.Name}.{methodName} carries no [Obsolete].");
  }
}
