using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Definition for the bare <c>[Collection("IiexFurnaceConfig")]</c> name <see cref="HeatBalanceTests"/>
/// carries: iiex's own suite defines the same name for its furnace-config tests
/// (<c>FurnaceConfigCollection</c>), but xUnit collections are scoped per assembly, so this suite needs
/// its own definition for <see cref="ExpandedLib.Testing.StaticStateCollection.EveryCollectionNameHasADefinition"/>
/// to pass here too.
/// </summary>
[CollectionDefinition("IiexFurnaceConfig", DisableParallelization = true)]
public class FurnaceConfigCollection { }
