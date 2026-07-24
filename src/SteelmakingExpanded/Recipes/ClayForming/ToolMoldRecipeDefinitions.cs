using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;

namespace SteelmakingExpanded.Recipes.ClayForming;

/// <summary>
/// Code-first clay-forming recipes for the raw tool molds (migrated from recipes/clayforming/{plate,
/// doubleingot,quadrod}mold.json). Each is a single recipe object: a full 14x14 base clay layer plus a second
/// layer whose cavity carves the mold shape. The layers are built from a solid-row + cavity-row helper so the
/// repetitive 14-wide voxel strings are authored once, not pasted per row.
/// </summary>
public class ToolMoldRecipeDefinitions : IExRecipeDefProvider
{
  private const string Full = "##############"; // a solid 14-wide clay row

  private static object ClayColor =>
    new
    {
      type = "item",
      code = "game:clay-*",
      name = "color",
      allowedVariants = new[] { "blue", "fire", "red" },
    };

  // 14 solid rows: the mold's base layer.
  private static string[] SolidLayer() => Enumerable.Repeat(Full, 14).ToArray();

  // A 14-row layer: solid top rows, then the cavity rows, then solid bottom rows (always summing to 14).
  private static string[] Layer(
    string cavityRow,
    int solidTop,
    int cavityRows
  ) =>
    Enumerable
      .Repeat(Full, solidTop)
      .Concat(Enumerable.Repeat(cavityRow, cavityRows))
      .Concat(Enumerable.Repeat(Full, 14 - solidTop - cavityRows))
      .ToArray();

  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      Mold(
        domain,
        "platemold",
        "Plate Mold",
        "plate",
        Layer("##__________##", 2, 10)
      ),
      Mold(
        domain,
        "doubleingotmold",
        "Double Ingot Mold",
        "doubleingot",
        Layer("##____##____##", 3, 8)
      ),
      Mold(
        domain,
        "quadrodmold",
        "Quad Rod Mold",
        "quadrod",
        Layer("#__#__##__#__#", 3, 8)
      ),
    ];

  private static ExRecipeDef Mold(
    string domain,
    string assetName,
    string name,
    string moldType,
    string[] cavityLayer
  ) =>
    ExRecipeDef
      .Create(domain, "clayforming", assetName)
      .Body(
        new
        {
          ingredient = ClayColor,
          pattern = new[] { SolidLayer(), cavityLayer },
          name,
          output = new
          {
            type = "block",
            code = $"smex:toolmold-{{color}}-raw-{moldType}",
          },
        }
      );
}
