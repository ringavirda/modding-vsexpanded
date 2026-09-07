using System;
using System.Reflection;
using System.Reflection.Emit;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="StaticStateCollection.EveryCollectionNameHasADefinition"/>: a bare
/// <c>[Collection("...")]</c> name with no matching <c>[CollectionDefinition(...)]</c> fails the
/// guard. Built as a throwaway dynamic assembly rather than a real fixture in this project, so the
/// planted bug never trips this suite's own copy of the guard (<see cref="CollectionGuardTests"/>).
/// </summary>
public class StaticStateCollectionTests {
  private static Assembly BuildAssembly(bool defineTheCollection) {
    var name = new AssemblyName(
      $"StaticStateCollectionTests.Dynamic.{Guid.NewGuid():N}"
    );
    AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(
      name,
      AssemblyBuilderAccess.Run
    );
    ModuleBuilder module = asm.DefineDynamicModule(name.Name!);

    ConstructorInfo collectionCtor =
      typeof(CollectionAttribute).GetConstructor([typeof(string)])!;
    TypeBuilder member = module.DefineType("Member", TypeAttributes.Public);
    member.SetCustomAttribute(
      new CustomAttributeBuilder(collectionCtor, ["Dynamic-Test-Collection"])
    );
    member.CreateType();

    if (defineTheCollection) {
      ConstructorInfo definitionCtor =
        typeof(CollectionDefinitionAttribute).GetConstructor([typeof(string)])!;
      TypeBuilder definition = module.DefineType(
        "CollectionDefinition",
        TypeAttributes.Public
      );
      definition.SetCustomAttribute(
        new CustomAttributeBuilder(definitionCtor, ["Dynamic-Test-Collection"])
      );
      definition.CreateType();
    }

    return asm;
  }

  [Fact]
  public void Fails_when_a_collection_name_has_no_definition() {
    Assembly withoutDefinition = BuildAssembly(defineTheCollection: false);

    var ex = Assert.Throws<InvalidOperationException>(() =>
      StaticStateCollection.EveryCollectionNameHasADefinition(withoutDefinition)
    );

    Assert.Contains("Dynamic-Test-Collection", ex.Message);
  }

  [Fact]
  public void Passes_when_every_collection_name_has_a_definition() {
    Assembly withDefinition = BuildAssembly(defineTheCollection: true);

    Exception? ex = Record.Exception(() =>
      StaticStateCollection.EveryCollectionNameHasADefinition(withDefinition)
    );

    Assert.Null(ex);
  }
}
