using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ExpandedLib.Testing;

/// <summary>
/// The pattern for serializing every test class that touches the same process-wide static state:
/// <code>
/// [CollectionDefinition(Name, DisableParallelization = true)]
/// public class SomeCollection { public const string Name = "Some"; }
/// </code>
/// A class in that collection then carries <c>[Collection(SomeCollection.Name)]</c> (or the bare
/// string literal - xUnit accepts either, matching by name). xUnit runs test classes in parallel by
/// default and a collection with no definition still exists implicitly, so a typo'd or copy-pasted
/// name silently starts its own collection instead of joining the one it meant to - two classes that
/// both mutate the same static field, each alone in its own "collection", racing on it. This type is
/// the guard against that: it fails when a <c>[Collection("X")]</c> name has no matching
/// <c>[CollectionDefinition("X", ...)]</c> anywhere in the assembly.
/// </summary>
public static class StaticStateCollection {
  /// <summary>
  /// Asserts that every xUnit collection name named by a <c>[Collection("...")]</c> attribute in
  /// <paramref name="assembly"/> has a matching <c>[CollectionDefinition("...", ...)]</c> in the same
  /// assembly. Call once per test assembly, from one <c>[Fact]</c>.
  /// </summary>
  /// <exception cref="InvalidOperationException">At least one collection name is used but never
  /// defined.</exception>
  public static void EveryCollectionNameHasADefinition(Assembly assembly) {
    var used = new HashSet<string>(StringComparer.Ordinal);
    var defined = new HashSet<string>(StringComparer.Ordinal);

    foreach (Type type in SafeTypes(assembly)) {
      foreach (string name in AttributeNames<CollectionAttribute>(type))
        used.Add(name);
      foreach (
        string name in AttributeNames<CollectionDefinitionAttribute>(type)
      )
        defined.Add(name);
    }

    var undefined = used.Except(defined)
      .OrderBy(n => n, StringComparer.Ordinal)
      .ToList();
    if (undefined.Count > 0)
      throw new InvalidOperationException(
        $"{undefined.Count} collection name(s) are used by [Collection(...)] but never declared by a "
          + $"[CollectionDefinition(...)] in {assembly.GetName().Name}, so each one silently runs as "
          + "its own unsynchronised collection: "
          + string.Join(", ", undefined)
      );
  }

  // Read through CustomAttributeData rather than the strongly-typed attribute instance: both
  // [Collection] and [CollectionDefinition] take the name as their sole positional constructor
  // argument, and reading it this way is immune to which xUnit attribute-property shape a given
  // package version exposes.
  private static IEnumerable<string> AttributeNames<TAttribute>(Type type)
    where TAttribute : Attribute =>
    CustomAttributeData
      .GetCustomAttributes(type)
      .Where(a => a.AttributeType == typeof(TAttribute))
      .Select(a => (string)a.ConstructorArguments[0].Value!);

  private static IEnumerable<Type> SafeTypes(Assembly assembly) {
    try {
      return assembly.GetTypes();
    } catch (ReflectionTypeLoadException ex) {
      return ex.Types.Where(t => t is not null).Select(t => t!);
    }
  }
}
