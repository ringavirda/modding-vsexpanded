using System;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using Newtonsoft.Json.Linq;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesBuildTimeFailuresTests {
  [Fact]
  public void A_role_on_a_glyph_the_legend_does_not_define_fails_the_build() {
    // The glyph is not in the drawing's alphabet, so without this the role would answer an empty set for
    // ever with no error anywhere.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*").Role('q', Flue).Layer(0, "C")
        )
    );
    Assert.Contains("'q'", ex.Message, StringComparison.Ordinal);
    Assert.Contains("Flue", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void A_role_on_a_glyph_the_drawing_never_uses_fails_the_build() {
    // The same empty set by the other route: legend entry present, glyph never drawn.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('v', "game:air")
            .Role('v', Flue)
            .Layer(0, "C")
        )
    );
    Assert.Contains("'v'", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void A_role_glyph_that_is_only_drawn_under_a_second_glyph_still_fails() {
    // A role two glyphs share is satisfied only when each glyph is drawn. Counting the role's cells instead
    // would let a drawing that lost one facing look complete.
    Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('t', "exlib:testtuyere*")
            .Legend('y', "exlib:testtuyere*")
            .Role('t', Tuyere)
            .Role('y', Tuyere)
            .Layer(0, "C t")
        )
    );
  }

  [Fact]
  public void One_glyph_can_carry_two_roles_and_its_cells_answer_to_both() {
    // A cell holds exactly one glyph, so two overlapping roles cannot be split across two glyphs. Stacking
    // both roles on one glyph is what lets a drawing state the shaft furnaces' crucible, where burden column
    // and metal pool are the same cells.
    JObject roles = (JObject)Attributes(OverlappingDef())["multiblockRoles"]!;

    // Both roles are emitted, sorted by key, each holding the same single cell: the `v` at (2,0,0). The
    // neighbouring `a` is the identical code and carries neither, so this is about the glyph rather than
    // about game:air.
    Assert.Equal(["Damper", "Flue"], roles.Properties().Select(p => p.Name));
    foreach (string role in new[] { "Damper", "Flue" }) {
      JToken cell = Assert.Single((JArray)roles[role]!);
      Assert.Equal(
        (2, 0, 0),
        ((int)cell["x"]!, (int)cell["y"]!, (int)cell["z"]!)
      );
    }
  }

  [Fact]
  public void Restating_a_glyphs_own_role_is_harmless() {
    // Role accumulation is a set, not a list: a role named twice on one glyph does not emit its cells twice.
    ExBlockDef def = ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('v', "game:air")
          .Role('v', Flue)
          .Role('v', Flue)
          .Layer(0, "C v v")
      );

    Assert.Equal(
      ["Flue"],
      ((JObject)Attributes(def)["multiblockRoles"]!)
        .Properties()
        .Select(p => p.Name)
    );
    Assert.Equal(
      2,
      ((JArray)Attributes(def)["multiblockRoles"]!["Flue"]!).Count
    );
  }

  [Fact]
  public void One_glyph_cannot_carry_two_codes() {
    // A cell gets one block number, so the second code would stop being required anywhere.
    Assert.Throws<ArgumentException>(() =>
      new MultiblockLayoutBuilder()
        .Legend('a', "game:air")
        .Legend('a', "exlib:testbrick*")
    );
  }

  [Fact]
  public void A_third_party_role_needs_nothing_from_exlib() {
    // "kiln-door" is minted here, not declared anywhere in exlib or in FurnaceCellRoles: the layout
    // builder and MultiblockCellRoles must accept and answer it on the strength of the key alone.
    CellRole kilnDoor = CellRole.Of("kiln-door");
    ExBlockDef def = ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Legend('C', "exlib:testmega*")
          .Legend('k', "game:air")
          .Role('k', kilnDoor)
          .Layer(0, "C k")
      );

    MultiblockCellRoles roles = MultiblockCellRoles.FromAttributes(
      new Vintagestory.API.Datastructures.JsonObject(Attributes(def))
    );
    Assert.Equal((1, 0, 0), Assert.Single(roles.CellsOf(kilnDoor)));
  }
}
