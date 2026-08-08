using System.Collections.Generic;
using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iwex's ore handling - a single machine, the <b>burdenmaker</b>.
/// <para>
/// The recipe is load-bearing: the burdenmaker is the only source of burden, so without it a fresh
/// survival world could never charge a furnace.
/// </para>
/// </summary>
public class OreProcessingRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [Burdenmaker(domain)];

  /// <summary>
  /// The burdenmaker's placed shell: <b>twelve same-colour fired bricks laid out as the machine's own 3 × 2
  /// floor plan</b>.
  /// <para>
  /// Cheap on purpose: what the grid buys is the basin floor the
  /// block is placed as, and every gram of the real cost sits in the five RCC stages after it (28 more
  /// bricks, 9 iron plates, 2 ingots). And it must stay <b>brick only</b> - this is the only source of
  /// burden, so anything it asks for is something a fresh survival world must already be able to make.
  /// </para>
  /// <para>
  /// The <c>brick</c> name binds one colour across all six cells and carries it into the output,
  /// so the machine is built in whatever colour was fed to it.
  /// </para>
  /// </summary>
  private static ExRecipeDef Burdenmaker(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "burdenmaker")
      .GridObject(r =>
        r.Pattern("BBB,BBB")
          .Size(3, 2)
          .Ingredient(
            "B",
            i =>
              i.Item("game:burnedbrick-*")
                .Named(
                  "brick",
                  "black",
                  "brown",
                  "cream",
                  "gray",
                  "orange",
                  "red",
                  "tan"
                )
                .Quantity(2)
          )
          .OutputBlock($"{domain}:burdenmaker-{{brick}}-n", 1)
      );

}
