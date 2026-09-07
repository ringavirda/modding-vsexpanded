using System;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="RegistryLawScanner"/> against a throwaway base type with two leaves in this assembly -
/// one that keeps the law and one that breaks it, so <c>ConcreteSubclasses</c> finds both and
/// <c>ForEach</c> reports only the one that fails.
/// </summary>
public class RegistryLawScannerTests {
  private abstract class Base {
    public abstract int Value { get; }
  }

  private sealed class GoodLeaf : Base {
    public override int Value => 1;
  }

  private sealed class BadLeaf : Base {
    public override int Value => -1;
  }

  [Fact]
  public void ConcreteSubclasses_finds_every_non_abstract_leaf_and_excludes_the_base() {
    var leaves = RegistryLawScanner.ConcreteSubclasses<Base>();

    Assert.Contains(typeof(GoodLeaf), leaves);
    Assert.Contains(typeof(BadLeaf), leaves);
    Assert.DoesNotContain(typeof(Base), leaves);
  }

  [Fact]
  public void ForEach_names_only_the_leaf_that_fails_the_law() {
    var ex = Assert.Throws<InvalidOperationException>(() =>
      RegistryLawScanner.ForEach<Base>(leaf => {
        var instance = (Base)Activator.CreateInstance(leaf)!;
        Assert.True(
          instance.Value > 0,
          $"{leaf.Name} has a non-positive Value"
        );
      })
    );

    Assert.Contains(nameof(BadLeaf), ex.Message);
    Assert.DoesNotContain(nameof(GoodLeaf), ex.Message);
  }
}
