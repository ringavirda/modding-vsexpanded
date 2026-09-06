using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Definition for the bare <c>[Collection("MetalRegistry")]</c> name <see cref="CastIronCatalogueTests"/>
/// carries: exlib's own suite defines the same name for its own metal-registry tests
/// (<c>MetalRegistryCollection</c>), but xUnit collections are scoped per assembly, so this suite needs
/// its own definition for <see cref="StaticStateCollection.EveryCollectionNameHasADefinition"/> to pass
/// here too.
/// </summary>
[CollectionDefinition("MetalRegistry", DisableParallelization = true)]
public class MetalRegistryCollection { }
