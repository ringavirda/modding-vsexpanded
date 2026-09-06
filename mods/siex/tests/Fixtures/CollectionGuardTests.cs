using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Every <c>[Collection("...")]</c> name used in this assembly has a matching
/// <c>[CollectionDefinition(...)]</c> - see <see cref="StaticStateCollection"/> for why a name with no
/// definition is a silent hole rather than a harmless default.
/// </summary>
public class CollectionGuardTests {
  [Fact]
  public void Every_collection_name_has_a_definition() =>
    StaticStateCollection.EveryCollectionNameHasADefinition(
      Assembly.GetExecutingAssembly()
    );
}
