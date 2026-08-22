using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The crucible hearth's exported shape. Nothing references it yet - the blocktype is U9.9's - so these
/// cases stand in for the def guards that will cover it later, and pin the two facts the code above it
/// will be written against: the element names it addresses and the one cell it occupies.
/// </summary>
public class CrucibleHearthShapeTests {
  private const string Path = "assets/iiex/shapes/furnace/cruciblehearth.json";

  private static JObject Shape() =>
    JObject.Parse(
      File.ReadAllText(
        DefinitionGoldens.SolutionRelative(Path.Replace('/', '\\'))
      )
    );

  /// <summary>Every element in the tree, as <c>Parent/Child</c> for a nested one.</summary>
  private static IEnumerable<string> Names(JToken? elements, string prefix = "") {
    foreach (JToken e in elements ?? new JArray()) {
      string name = (string)e["name"]!;
      yield return prefix + name;
      foreach (string child in Names(e["children"], prefix + name + "/"))
        yield return child;
    }
  }

  /// <summary>
  /// The groups the block entity will pose: the four pots, what stands in them, their covers and the fuel
  /// bed. Named rather than counted, because each one is a selector the code will spell out and a rename
  /// in the editable would otherwise surface as a pot that silently stops rendering.
  /// </summary>
  [Fact]
  public void The_hearth_carries_the_groups_the_code_will_name() {
    var names = Names(Shape()["elements"]).ToHashSet();

    Assert.Contains("Base/Masonry", names);
    Assert.All(
      new[] { 1, 2, 3 },
      n => Assert.Contains($"Base/Firebar{n}", names)
    );
    foreach (int n in new[] { 1, 2, 3, 4 }) {
      Assert.Contains($"Base/ClayStand{n}", names);
      Assert.Contains($"Crucibles/Crucible{n}", names);
      Assert.Contains($"FillingBlisterSteel/BlisterSteel{n}", names);
      Assert.Contains($"FillingSlag/Slag{n}", names);
      Assert.Contains($"Covers/Cover{n}", names);
    }
  }

  /// <summary>
  /// The fuel bed is named exactly as the firebox names its own, so the hearth reuses that mesher
  /// unchanged rather than carrying a second copy of the same six-course selector.
  /// </summary>
  [Fact]
  public void The_fuel_bed_matches_what_the_firebox_mesher_emits() {
    var names = Names(Shape()["elements"]).ToHashSet();
    List<string> full = BlockFirebox.ElementsFor(
      BEBehaviorFirebox.LayersPerCell
    );

    // The base element the firebox always keeps is its own, not the hearth's; the courses are the shared
    // part, and they are what has to line up.
    Assert.All(
      full.Where(e => e.Contains("CokeL")),
      element => Assert.Contains(element, names)
    );
    Assert.Equal(6, names.Count(n => n.StartsWith("Coke/CokeL")));
  }

  /// <summary>
  /// One cell, holding all four pots - not a row of holes. A hearth that had grown past its cell would
  /// need a footprint and a filler, and would stop being the single block the layout draws.
  /// </summary>
  [Fact]
  public void The_hearth_is_a_single_cell() {
    double[] lo = [16, 16, 16];
    double[] hi = [0, 0, 0];

    void Walk(JToken? elements) {
      foreach (JToken e in elements ?? new JArray()) {
        if (e["from"] is JArray from && e["to"] is JArray to)
          for (int i = 0; i < 3; i++) {
            lo[i] = System.Math.Min(
              lo[i],
              System.Math.Min((double)from[i], (double)to[i])
            );
            hi[i] = System.Math.Max(
              hi[i],
              System.Math.Max((double)from[i], (double)to[i])
            );
          }
        Walk(e["children"]);
      }
    }
    Walk(Shape()["elements"]);

    // A voxel either side is the drawn overhang every furnace part carries; a second cell would be 16.
    Assert.Equal([-1d, -1d, 0d], lo);
    Assert.Equal([16d, 14d, 16d], hi);
  }

  /// <summary>
  /// The export resolved every texture to an asset code. An editable names them by absolute path into the
  /// artist's checkout, and one that survives renders the missing-texture checker with nothing in any log.
  /// </summary>
  [Fact]
  public void Every_texture_resolved_to_an_asset_code() {
    foreach (JProperty texture in ((JObject)Shape()["textures"]!).Properties()) {
      string value = (string)texture.Value!;
      Assert.Contains(':', value);
      Assert.DoesNotContain(":/", value);
    }
  }
}
