using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Collection definitions for the process-wide static registries that were previously only named by a
/// bare <c>[Collection("...")]</c> string literal - which serializes their own members against each
/// other, but gives <see cref="StaticStateCollection.EveryCollectionNameHasADefinition"/> nothing to
/// confirm the name was not a typo. One definition per registry; members are named on each.
/// </summary>
[CollectionDefinition("MetalRegistry", DisableParallelization = true)]
public class MetalRegistryCollection {
  // Members: MetalRegistryTests (writes it), MetalCatalogueLoaderTests (shares it),
  // CatalogueContributorsTests (shares it).
}

[CollectionDefinition("MaterialRoles", DisableParallelization = true)]
public class MaterialRolesCollection {
  // Members: MaterialRoleRegistryTests (writes it), MaterialRoleLoaderTests (shares it).
}

[CollectionDefinition("ExLiquids", DisableParallelization = true)]
public class ExLiquidsCollection {
  // Members: ExLiquidsLoaderTests (writes it), MediumTaxonomyTests (shares it).
}

[CollectionDefinition("ExDefinitions", DisableParallelization = true)]
public class ExDefinitionsCollection {
  // Members: ExDefinitionDiscoveryTests, ExDefinitionInjectionTests, ExDefinitionsTests.
}
