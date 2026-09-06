using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Catalogues;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Items generated from the stage catalogue. A stage that names a <c>code</c> is a stopping point, and a
/// stopping point is an item - so the rolled catalogue stops being hand-authored defs and falls out of the
/// declaration instead. Emitted at inject time, which is why the routes are config assets rather than item
/// attributes. See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessItemEmitterTests {
  private static ProcessRoute Route(string json) {
    Assert.True(
      ProcessRoute.TryParse(
        new JsonObject(JToken.Parse(json)),
        out ProcessRoute? route,
        out string? error
      ),
      error
    );
    return route!;
  }

  private const string BarRoute = """
    {
      "family": "shingledbar",
      "shape": "iiex:item/smithed/shingled-bar",
      "stages": [
        { "thickness": 3.0, "element": "ShingledBar1", "acceptedBy": [ "grooved" ] },
        { "thickness": 2.0, "element": "Grooved200", "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" }
      ]
    }
    """;

  private static List<ExItemDef> Emit(params string[] routes) =>
    [.. ProcessItemEmitter.Emit(routes.Select(Route), out _)];

  private static JObject Json(ExItemDef def) => def.ToJson();

  #region What becomes an item

  [Fact]
  public void A_stage_that_names_a_code_becomes_an_item() {
    ExItemDef def = Assert.Single(Emit(BarRoute));

    Assert.Equal("rolledrod", Json(def)["code"]!.ToString());
    Assert.Equal("iiex", def.Location.Domain);
  }

  [Fact]
  public void A_stage_with_no_code_is_a_render_only_intermediate_and_builds_nothing() {
    // The 3.0 entry stage is a state the piece passes through, not a thing it becomes.
    Assert.Single(Emit(BarRoute));
  }

  [Fact]
  public void The_owning_domain_comes_from_the_declared_code() {
    // A modder's products land in their own domain, not ours, whoever's route they extend.
    ExItemDef def = Assert.Single(
      Emit(BarRoute.Replace("iiex:rolledrod", "othermod:splinerod"))
    );

    Assert.Equal("othermod", def.Location.Domain);
    Assert.Equal("splinerod", Json(def)["code"]!.ToString());
  }

  [Fact]
  public void A_stage_can_opt_out_and_build_nothing() {
    // "The code exists already; wire it up, build nothing" - how a declaration points at an item the mod
    // ships itself.
    Assert.Empty(
      Emit(
        BarRoute.Replace(
          "\"code\": \"iiex:rolledrod\"",
          "\"code\": \"iiex:rolledrod\", \"generate\": false"
        )
      )
    );
  }

  [Fact]
  public void A_code_in_the_vanilla_domain_is_never_generated_over() {
    // Injecting an itemtype into `game:` would replace one of the base game's own items. A declaration
    // pointing at a vanilla item is wiring it up, never building it.
    List<ExItemDef> defs =
    [
      .. ProcessItemEmitter.Emit(
        [Route(BarRoute.Replace("iiex:rolledrod", "game:rod-iron"))],
        out List<string> skipped
      ),
    ];

    Assert.Empty(defs);
    Assert.Contains("game:rod-iron", Assert.Single(skipped));
  }

  [Fact]
  public void A_code_with_no_path_is_skipped_rather_than_failing_the_load() {
    // A code is a modder's free text. A blank one never reaches here - the parser reads it as "no code",
    // so the stage is simply not a stopping point - but a domain with nothing after it does.
    List<ExItemDef> defs =
    [
      .. ProcessItemEmitter.Emit(
        [Route(BarRoute.Replace("\"iiex:rolledrod\"", "\"iiex:\""))],
        out List<string> skipped
      ),
    ];

    Assert.Empty(defs);
    Assert.Single(skipped);
  }

  [Fact]
  public void A_blank_code_is_not_a_stopping_point_at_all() {
    Assert.Empty(Emit(BarRoute.Replace("\"iiex:rolledrod\"", "\"   \"")));
  }

  [Fact]
  public void One_code_declared_twice_yields_one_item() {
    // A fork can reach the same product down two branches, and the object loader would reject a duplicate
    // itemtype.
    Assert.Single(
      Emit(
        BarRoute,
        BarRoute.Replace(
          "\"family\": \"shingledbar\"",
          "\"family\": \"castbillet\""
        )
      )
    );
  }

  #endregion

  #region What the generated item looks like

  [Fact]
  public void The_item_is_drawn_by_its_own_element_of_the_family_shape() {
    // The route names one shape file for the family and the stage names its element in it, so the
    // generated item renders as the stage the piece stopped at.
    JObject shape = (JObject)Json(Assert.Single(Emit(BarRoute)))["shape"]!;

    Assert.Equal("iiex:item/smithed/shingled-bar", shape["base"]!.ToString());
    Assert.Equal(
      ["Grooved200"],
      shape["selectiveElements"]!.Select(e => e.ToString())
    );
  }

  [Fact]
  public void A_stage_with_no_element_takes_the_whole_shape_file() {
    JObject shape = (JObject)
      Json(
        Assert.Single(
          Emit(BarRoute.Replace("\"element\": \"Grooved200\", ", ""))
        )
      )["shape"]!;

    Assert.Equal("iiex:item/smithed/shingled-bar", shape["base"]!.ToString());
    Assert.Null(shape["selectiveElements"]);
  }

  [Fact]
  public void A_sparse_declaration_still_yields_a_working_item() {
    // No shape anywhere: the item must still load and be reachable, or a modder's first attempt is a
    // crash rather than an untextured cube.
    ExItemDef def = Assert.Single(
      Emit(
        """
        {
          "family": "bronzebar",
          "stages": [ { "thickness": 1.0, "acceptedBy": [ "flat" ], "code": "othermod:bronzeplate" } ]
        }
        """
      )
    );

    JObject json = Json(def);
    Assert.NotNull(json["shape"]);
    Assert.NotNull(json["creativeinventory"]);
    Assert.NotNull(json["maxstacksize"]);
  }

  #endregion

  #region The buildable set

  [Fact]
  public void The_generated_codes_are_public_so_a_guard_can_check_them() {
    Assert.Equal(
      ["iiex:rolledrod"],
      ProcessItemEmitter.GeneratedCodes([Route(BarRoute)])
    );
  }

  [Fact]
  public void An_opted_out_stage_is_not_in_the_generated_set() {
    Assert.Empty(
      ProcessItemEmitter.GeneratedCodes([
        Route(
          BarRoute.Replace(
            "\"code\": \"iiex:rolledrod\"",
            "\"code\": \"iiex:rolledrod\", \"generate\": false"
          )
        ),
      ])
    );
  }

  #endregion
}
