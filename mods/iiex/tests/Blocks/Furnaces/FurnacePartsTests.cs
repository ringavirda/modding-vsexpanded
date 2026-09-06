using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.Items;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The furnace parts a multiblock is built out of, and the two failure modes neither the build nor the
/// runtime reports: a layout naming a block no blocktype defines, which yields a structure that can never
/// be completed, and a layout naming a shape element the art does not have, which draws nothing where a
/// charge should be.
/// </summary>
public class FurnacePartsTests {
  private static Shape LoadShape(string name) {
    string path = Path.Combine(
      RepoPaths.Assets("iiex"),
      "shapes",
      "furnace",
      $"{name}.json"
    );
    return JsonConvert.DeserializeObject<Shape>(File.ReadAllText(path))!;
  }

  private static HashSet<string> PathsOf(string shapeName) =>
    [.. ExShapeElements.AllPaths(LoadShape(shapeName))];

  #region Every layout code resolves to a real block

  [Fact]
  public void Every_multiblock_layout_code_names_a_block_that_exists() {
    // A layout cell naming a code that is not in the registry becomes a blockNumbers entry matching
    // nothing, so the structure can never complete. Nothing else reports it.
    IReadOnlyList<string> missing = MultiblockCodes.Unresolvable(
      out int checkedCodes,
      (
        "iiex",
        typeof(BlockStructures.Furnaces.Blocks.BlockPuddlingHearth).Assembly
      ),
      ("exlib", typeof(ExpandedLib.Structures.StructureFillers).Assembly)
    );
    Assert.True(
      missing.Count == 0,
      "Layout codes with no block behind them:\n" + string.Join("\n", missing)
    );

    // Guards against the collection silently yielding nothing. iiex ships six structures (cold blast,
    // cupola, puddling, heating, coke oven, crucible), each naming several mod-domain parts, so 18 is a
    // safe floor.
    Assert.True(
      checkedCodes >= 18,
      $"only {checkedCodes} layout codes examined - is collection broken?"
    );
  }

  [Fact]
  public void No_layout_pins_a_network_nodes_orientation() {
    // The half the builder cannot judge: `iiex:furnace-tuyere-n` reads exactly like
    // `iiex:hopper-tall-e`, and only the referenced def says which of the two re-picks its own
    // orientation. A pinned node can be contradicted at any moment - Connector is what a layout should
    // say instead.
    IReadOnlyList<string> pinned = PinnedNetworkNodes.Violations(
      out int checkedCodes,
      (
        "iiex",
        typeof(BlockStructures.Furnaces.Blocks.BlockPuddlingHearth).Assembly
      ),
      ("exlib", typeof(ExpandedLib.Structures.StructureFillers).Assembly)
    );
    Assert.True(
      pinned.Count == 0,
      "Layouts pinning a network node:\n" + string.Join("\n", pinned)
    );
    Assert.True(
      checkedCodes > 0,
      "no pinned codes examined - is collection broken?"
    );
  }

  #endregion

  #region Hearth element names match the shipped art

  [Theory]
  [InlineData(HearthRows.Row.Left)]
  [InlineData(HearthRows.Row.Centre)]
  [InlineData(HearthRows.Row.Right)]
  public void Every_puddling_hearth_element_exists_in_the_shape(
    HearthRows.Row row
  ) {
    HashSet<string> art = PathsOf("puddlinghearth");
    Assert.Contains(PuddlingHearthLayout.FettleElement(row), art);
    for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
      Assert.Contains(PuddlingHearthLayout.PigElement(row, i), art);
  }

  [Fact]
  public void Every_heating_hearth_element_exists_in_the_shape() {
    HashSet<string> art = PathsOf("heatinghearth");
    foreach (HearthRows.Row row in HearthRows.All)
      foreach (
        HeatingHearthLayout.Stock stock in Enum.GetValues<HeatingHearthLayout.Stock>()
      ) {
        // Element() ends in the "/*" subtree marker; the art holds the bare path.
        string el = HeatingHearthLayout.Element(row, stock)[..^2];
        Assert.Contains(el, art);
      }
  }

  /// <summary>
  /// The bath elements the melted state names. <c>ExShapeElements.Pruned</c> drops an unknown name
  /// without raising anything, so a typo here is an invisible hole in the mesh rather than an error - and
  /// the melted bed would draw as bare fettling with the charge simply gone.
  /// </summary>
  [Fact]
  public void The_bath_group_exists_in_the_shipped_hearth_shape() {
    HashSet<string> art = PathsOf("puddlinghearth");

    // ElementsFor emits the "/*" subtree marker; the art holds the bare group path.
    Assert.Contains(PuddlingHearthLayout.BathElement[..^2], art);
    // One slab per bed cell, or the bath covers part of a bed the pigs covered all of.
    Assert.Equal(
      HearthRows.All.Length,
      art.Count(p => p.StartsWith("Bath/", StringComparison.Ordinal))
    );
  }

  [Fact]
  public void The_structural_groups_every_hearth_always_draws_exist() {
    foreach (string shape in new[] { "puddlinghearth", "heatinghearth" }) {
      HashSet<string> art = PathsOf(shape);
      foreach (string group in new[] { "Base", "BaseExtension", "Bed" })
        Assert.Contains(group, art);
    }
  }

  [Fact]
  public void The_nine_pig_elements_are_distinct_and_cover_the_whole_charge() {
    var all = new HashSet<string>();
    foreach (HearthRows.Row row in HearthRows.All)
      for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
        all.Add(PuddlingHearthLayout.PigElement(row, i));
    // A name collision would draw two pigs in one place and leave a hole where the third belongs.
    Assert.Equal(PuddlingHearthLayout.PigCapacity, all.Count);
  }

  [Fact]
  public void The_row_element_groups_are_not_in_name_order() {
    // Items1/2/3 have byte-identical children and are positioned by their own group `from`, so the
    // numbering carries no order: Items3 is the centre, not the right. Reordering the mapping into
    // 1/2/3 shifts every loaded piece one cell without raising anything.
    Assert.Equal("Items1", HearthRows.ElementGroup(HearthRows.Row.Left));
    Assert.Equal("Items3", HearthRows.ElementGroup(HearthRows.Row.Centre));
    Assert.Equal("Items2", HearthRows.ElementGroup(HearthRows.Row.Right));

    // The same holds for the puddling hearth's fettle beds: Cube11 is the centre, Cube13 the left.
    Assert.Equal(
      "Fettle/Cube13",
      PuddlingHearthLayout.FettleElement(HearthRows.Row.Left)
    );
    Assert.Equal(
      "Fettle/Cube11",
      PuddlingHearthLayout.FettleElement(HearthRows.Row.Centre)
    );
    Assert.Equal(
      "Fettle/Cube12",
      PuddlingHearthLayout.FettleElement(HearthRows.Row.Right)
    );
  }

  #endregion

  #region The access rule

  [Fact]
  public void A_loaded_centre_blocks_both_flanks_but_never_itself() {
    Assert.True(HearthRows.CanReach(HearthRows.Row.Centre, centreLoaded: true));
    Assert.False(HearthRows.CanReach(HearthRows.Row.Left, centreLoaded: true));
    Assert.False(HearthRows.CanReach(HearthRows.Row.Right, centreLoaded: true));

    Assert.Equal(3, HearthRows.Reachable(centreLoaded: false).Count());
    Assert.Equal(
      [HearthRows.Row.Centre],
      HearthRows.Reachable(centreLoaded: true)
    );
  }

  [Theory]
  [InlineData(-1, HearthRows.Row.Left)]
  [InlineData(0, HearthRows.Row.Centre)]
  [InlineData(1, HearthRows.Row.Right)]
  public void A_cell_offset_picks_its_row(int x, HearthRows.Row expected) =>
    Assert.Equal(
      expected,
      HearthRows.FromLocalOffset(new Vintagestory.API.MathTools.Vec3i(x, 0, 0))
    );

  [Fact]
  public void An_offset_off_the_bed_is_no_row_at_all() {
    // Two cells out, or a cell above, is off the bed. Returning a row for these would let a click on an
    // unrelated filler charge the hearth.
    Assert.Null(
      HearthRows.FromLocalOffset(new Vintagestory.API.MathTools.Vec3i(2, 0, 0))
    );
    Assert.Null(
      HearthRows.FromLocalOffset(new Vintagestory.API.MathTools.Vec3i(0, 1, 0))
    );
  }

  #endregion

  #region Stock recognition

  [Theory]
  [InlineData("stock-shingledbar", HeatingHearthLayout.Stock.ShingledBloom)]
  [InlineData("stock-shingledslab", HeatingHearthLayout.Stock.ShingledSlab)]
  [InlineData("caststock-billet", HeatingHearthLayout.Stock.CastBillet)]
  [InlineData("caststock-bloom", HeatingHearthLayout.Stock.CastBloom)]
  [InlineData("caststock-slab", HeatingHearthLayout.Stock.CastSlab)]
  public void The_hearth_recognises_every_form_it_has_a_bed_for(
    string path,
    HeatingHearthLayout.Stock want
  ) => Assert.Equal(want, HeatingHearthLayout.StockOf(path));

  [Fact]
  public void Every_cast_stock_code_the_mod_actually_ships_is_recognised() {
    // The rows above are literals, and literals are what let the recogniser go on naming `castbillet`
    // for months after the item became `caststock-billet` - a stale prefix matches nothing, so no cast
    // piece could be reheated and every test still passed. This reads the shipped variant list instead.
    foreach ((string form, _, _) in CastStockItemDefinitions.Forms)
      Assert.True(
        HeatingHearthLayout.StockOf($"caststock-{form}") != null,
        $"the hearth does not recognise the shipped caststock-{form}"
      );
  }

  [Theory]
  [InlineData("pig")]
  [InlineData("ingot-castiron")]
  [InlineData("")]
  [InlineData(null)]
  public void Anything_the_hearth_cannot_reheat_is_refused(string? path) =>
    Assert.Null(HeatingHearthLayout.StockOf(path));

  #endregion
}
