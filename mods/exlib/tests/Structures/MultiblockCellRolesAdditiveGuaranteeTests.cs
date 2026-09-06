using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesAdditiveGuaranteeTests {
  [Fact]
  public void A_layout_with_no_roles_emits_no_roles_attribute() {
    // A layout with no Role() calls emits no attribute, so goldens of role-less structures do not move.
    Assert.Null(Attributes(UnroledDef())["multiblockRoles"]);
  }

  [Fact]
  public void Adding_roles_changes_nothing_about_the_structure_a_layout_emits() {
    // The same drawing with and without Role() calls emits byte-identical multiblockStructure JSON. Roles
    // are metadata beside the layout, never part of it.
    Assert.Equal(
      Attributes(UnroledDef())["multiblockStructure"]!.ToString(
        Newtonsoft.Json.Formatting.None
      ),
      Attributes(RoledDef())["multiblockStructure"]!.ToString(
        Newtonsoft.Json.Formatting.None
      )
    );
  }

  [Fact]
  public void Numbers_are_still_handed_out_in_legend_declaration_order() {
    // Per-code numbering must be the identity for a layout with one glyph per code, which is every shipped
    // layout, or their goldens churn.
    var numbers = (JObject)
      Attributes(
        ExBlockDef
          .Create("exlib", "testmega")
          .MultiblockLayout(s =>
            s.Legend('C', "exlib:testmega*")
              .Legend('#', "exlib:testbrick*")
              .Legend('a', "game:air")
              .Layer(0, "C # a")
          )
      )["multiblockStructure"]!["blockNumbers"]!;

    Assert.Equal(
      """{"exlib:testmega*":1,"exlib:testbrick*":2,"game:air":3}""",
      numbers.ToString(Newtonsoft.Json.Formatting.None)
    );
  }

  [Fact]
  public void A_structure_whose_layout_declares_no_roles_answers_every_role_empty() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, UnroledDef()).Complete();

    foreach (CellRole role in new[] { Chargeable, Firebox, Tuyere, MetalTap, SlagTap, Flue, Damper })
      Assert.Empty(machine.CellsWithRole(role));
    // The footprint is non-empty beside it, so this cannot pass by the machine having failed to load a
    // layout at all.
    Assert.NotEmpty(machine.CellsAccepting(new AssetLocation("game:air")));
  }
}
