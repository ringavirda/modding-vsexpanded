using System;
using System.Linq;
using System.Reflection;
using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Metals;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The naming law every catalogue registry follows (docs/design/conventions.md, "Catalogue registry
/// verbs"): <c>Register</c> declares from code, <c>Contribute</c> merges a parsed set, <c>Load</c> (on
/// the loader, never the registry) reads assets in, <c>Clear</c> empties, <c>Contributors</c> is the
/// hook a mod's C# survives a reload through. Scoped to the six catalogue registries
/// (<c>ExpandedLib.Catalogues</c> plus <see cref="MetalRegistry"/>, the one catalogue registry the
/// family namespace carries) rather than every type merely named <c>*Registry</c>: the framework also
/// has registration registries with no catalogue to load or clear (<c>EntityRegistry</c>,
/// <c>CommandRegistry</c>, <c>PreferenceRegistry</c>), and they are a different contract.
/// </summary>
public class CatalogueNamingTests {
  private static Type[] CatalogueRegistryTypes() {
    Assembly asm = typeof(ExpandedLibModSystem).Assembly;
    Type[] byNamespace =
    [
      .. asm.GetTypes()
        .Where(t =>
          t.IsPublic
          && t.Name.EndsWith("Registry", StringComparison.Ordinal)
          && (t.Namespace ?? "").StartsWith(
            "ExpandedLib.Catalogues",
            StringComparison.Ordinal
          )
        ),
    ];
    return [.. byNamespace, typeof(MetalRegistry), typeof(ExLiquids)];
  }

  [Fact]
  public void Every_catalogue_registry_exposes_Clear_and_Contributors() {
    foreach (Type type in CatalogueRegistryTypes()) {
      Assert.True(
        type.GetMethod(
          "Clear",
          BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
        ) != null,
        $"{type.Name} has no public Clear()"
      );
      Assert.True(
        type.GetProperty(
          "Contributors",
          BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
        ) != null,
        $"{type.Name} has no public Contributors"
      );
    }
  }

  [Fact]
  public void No_catalogue_registry_exposes_an_Add_or_Load_member() {
    foreach (Type type in CatalogueRegistryTypes())
      foreach (
        MemberInfo member in type.GetMembers(
          BindingFlags.Public
            | BindingFlags.Static
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly
        )
      ) {
        // Skip the [Obsolete] forwarder itself: the naming law is being enforced by keeping it
        // deprecated, not by hiding that it once existed.
        if (member.GetCustomAttribute<ObsoleteAttribute>() != null)
          continue;
        Assert.False(
          member.Name.StartsWith("Add", StringComparison.Ordinal),
          $"{type.Name}.{member.Name} starts with 'Add' - Contribute is the verb for merging a parsed set"
        );
        Assert.False(
          member.Name.StartsWith("Load", StringComparison.Ordinal),
          $"{type.Name}.{member.Name} starts with 'Load' - reading assets is the loader's job, not the registry's"
        );
      }
  }
}
