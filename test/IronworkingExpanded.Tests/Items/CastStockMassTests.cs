using System.Linq;
using IronworkingExpanded.Items;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The cast-stock masses and the ladder arithmetic that depends on them: the three numbers must agree
/// with the crop table in <c>iwex.md</c>, and the constants must not drift from the
/// <see cref="CastStockItemDefinitions.Forms"/> table. Asserts nothing about voxel counts - the item
/// shapes and the long cell's sand cavities draw the rammed sand and the fill glow, not metal content.
/// </summary>
public class CastStockMassTests {
  #region The table is the single source

  [Fact]
  public void The_forms_table_is_the_single_source_the_constants_come_from() {
    // The constants name the masses for callers; the table generates the long cell's lanes and the item
    // defs from one row each. They must not disagree.
    Assert.Equal(
      [
        CastStockItemDefinitions.BilletUnits,
        CastStockItemDefinitions.BloomUnits,
        CastStockItemDefinitions.SlabUnits,
      ],
      CastStockItemDefinitions.Forms.Select(f => f.Units)
    );
  }

  [Theory]
  [InlineData("billet", CastStockItemDefinitions.BilletUnits)]
  [InlineData("bloom", CastStockItemDefinitions.BloomUnits)]
  [InlineData("slab", CastStockItemDefinitions.SlabUnits)]
  [InlineData("frame", 0)] // a long-cell pattern, but a machine part rather than stock
  [InlineData("pig", 0)]
  public void UnitsOf_answers_only_for_cast_stock(string form, int expected) =>
    Assert.Equal(expected, CastStockItemDefinitions.UnitsOf(form));

  #endregion

  #region The ladder divides

  [Theory]
  // 600 u is the wide tier's quantum; these are the divisions iwex.md's crop table is built on. A mass
  // that fails one of them strands a remainder in every crop.
  [InlineData(CastStockItemDefinitions.BilletUnits, 200, 3)] // 3 plate @200, at 9 long (27 = 3 x 9)
  [InlineData(CastStockItemDefinitions.BilletUnits, 100, 6)] // 6 rolledrod @100
  [InlineData(CastStockItemDefinitions.BloomUnits, 200, 5)] // 5 plate @200
  [InlineData(CastStockItemDefinitions.SlabUnits, 600, 5)] // 5 heavyplate or boilerplate @600
  public void Each_stock_crops_into_a_whole_number_of_products(
    int stock,
    int product,
    int count
  ) {
    Assert.Equal(0, stock % product);
    Assert.Equal(count, stock / product);
  }

  #endregion
}
