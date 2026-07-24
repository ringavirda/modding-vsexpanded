using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Unit coverage for the recipe-side builders <see cref="ExRecipeDef"/> / <see cref="GridRecipeBuilder"/> /
/// <see cref="IngredientBuilder"/>: the file location targets <c>recipes/{category}/</c>, grid recipes emit the
/// loader's schema, and optional ingredient/output keys are absent unless set (so the emitted JSON matches the
/// hand-written form exactly). Pure (no registry), so no serialization collection is needed.
/// </summary>
public class ExRecipeDefTests
{
  [Fact]
  public void Location_targets_the_recipes_category_folder()
  {
    ExRecipeDef def = ExRecipeDef.Create("lpex", "grid", "pipes");
    Assert.Equal("lpex", def.Domain);
    Assert.Equal("grid", def.Category);
    Assert.Equal("pipes", def.Code);
    Assert.Equal("lpex", def.Location.Domain);
    Assert.Equal("recipes/grid/pipes.json", def.Location.Path);
  }

  [Fact]
  public void ToJson_is_an_array_of_the_added_recipes_in_order()
  {
    ExRecipeDef def = ExRecipeDef
      .Create("lpex", "grid", "x")
      .Grid(r => r.Name("first").Pattern("P").Size(1, 1).OutputItem("game:a"))
      .Grid(r => r.Name("second").Pattern("P").Size(1, 1).OutputItem("game:b"));

    var arr = (JArray)def.ToJson();
    Assert.Equal(2, arr.Count);
    Assert.Equal("first", (string?)arr[0]["name"]);
    Assert.Equal("second", (string?)arr[1]["name"]);
    Assert.Equal(2, def.Count);
  }

  [Fact]
  public void Grid_recipe_emits_the_full_loader_schema()
  {
    var recipe = (JObject)
      ((JArray)
        ExRecipeDef
          .Create("lpex", "grid", "x")
          .Grid(r =>
            r.Name("Piping (Straight)")
              .Pattern("HPN")
              .Size(3, 1)
              .Ingredient("P", i => i.Item("game:metalplate-*").Metal().Quantity(1))
              .Ingredient("H", i => i.Item("game:hammer-*").Tool())
              .OutputBlock("lpex:pipe-straight-ns-{metal}", 2)
          )
          .ToJson())[0];

    Assert.Equal("Piping (Straight)", (string?)recipe["name"]);
    Assert.Equal("HPN", (string?)recipe["ingredientPattern"]);
    Assert.Equal(3, (int)recipe["width"]!);
    Assert.Equal(1, (int)recipe["height"]!);
    Assert.Equal("item", (string?)recipe["ingredients"]!["P"]!["type"]);
    Assert.Equal("game:metalplate-*", (string?)recipe["ingredients"]!["P"]!["code"]);
    Assert.Equal("metal", (string?)recipe["ingredients"]!["P"]!["name"]);
    Assert.Equal(["iron", "steel"], recipe["ingredients"]!["P"]!["allowedVariants"]!.ToObject<string[]>());
    Assert.Equal(1, (int)recipe["ingredients"]!["P"]!["quantity"]!);
    Assert.Equal("block", (string?)recipe["output"]!["type"]);
    Assert.Equal("lpex:pipe-straight-ns-{metal}", (string?)recipe["output"]!["code"]);
    Assert.Equal(2, (int)recipe["output"]!["quantity"]!);
  }

  [Fact]
  public void Tool_ingredient_is_marked_and_omits_quantity()
  {
    var ing = (JObject)
      ((JArray)
        ExRecipeDef
          .Create("lpex", "grid", "x")
          .Grid(r => r.Pattern("H").Size(1, 1).Ingredient("H", i => i.Item("game:hammer-*").Tool()).OutputItem("game:a"))
          .ToJson())[0]["ingredients"]!["H"]!;

    Assert.True((bool)ing["isTool"]!);
    Assert.Null(ing["quantity"]);
    Assert.Null(ing["name"]);
  }

  [Fact]
  public void Output_omits_quantity_when_not_supplied()
  {
    var output = (JObject)
      ((JArray)
        ExRecipeDef
          .Create("lpex", "grid", "x")
          .Grid(r => r.Pattern("P").Size(1, 1).OutputBlock("lpex:pipe-bend-nw-{metal}"))
          .ToJson())[0]["output"]!;

    Assert.Equal("lpex:pipe-bend-nw-{metal}", (string?)output["code"]);
    Assert.Null(output["quantity"]);
  }

  [Fact]
  public void GridObject_emits_a_single_recipe_object_not_an_array()
  {
    // A one-recipe file the source authored as a lone object (bunker/molten-barrel) - the token must be an
    // OBJECT, not a one-element array, so it is byte-faithful to the source.
    ExRecipeDef def = ExRecipeDef
      .Create("iwex", "grid", "bunker")
      .GridObject(r => r.Pattern("B").Size(1, 1).OutputBlock("iwex:bunker-{brick}-north", 1));

    JToken json = def.ToJson();
    Assert.Equal(JTokenType.Object, json.Type);
    Assert.Equal("B", (string?)json["ingredientPattern"]);
    Assert.Equal(1, def.Count);
  }

  [Fact]
  public void Body_sets_an_arbitrary_single_object_and_mixing_modes_throws()
  {
    ExRecipeDef def = ExRecipeDef
      .Create("smex", "barrel", "mortar")
      .Body(new { code = "mortarfromslag", output = new { type = "item", code = "game:mortar", stackSize = 4 } });
    Assert.Equal(JTokenType.Object, def.ToJson().Type);
    Assert.Equal("mortarfromslag", (string?)def.ToJson()["code"]);

    // Array + single-object modes are mutually exclusive.
    Assert.Throws<System.InvalidOperationException>(() =>
      ExRecipeDef.Create("x", "grid", "y").Grid(r => r.Pattern("P").Size(1, 1)).Body(new { a = 1 })
    );
    Assert.Throws<System.InvalidOperationException>(() =>
      ExRecipeDef.Create("x", "grid", "y").Body(new { a = 1 }).Grid(r => r.Pattern("P").Size(1, 1))
    );
  }

  [Fact]
  public void Add_appends_an_arbitrary_recipe_object()
  {
    // The escape hatch for recipe types without a dedicated builder (barrel/clayforming/smithing).
    var arr = (JArray)
      ExRecipeDef
        .Create("smex", "barrel", "mortar")
        .Add(new { code = "mortarfromslag", output = new { type = "item", code = "game:mortar", stackSize = 4 } })
        .ToJson();

    Assert.Single(arr);
    Assert.Equal("mortarfromslag", (string?)arr[0]["code"]);
    Assert.Equal(4, (int)arr[0]["output"]!["stackSize"]!);
  }
}
