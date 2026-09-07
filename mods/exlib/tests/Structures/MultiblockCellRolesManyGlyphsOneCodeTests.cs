using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesManyGlyphsOneCodeTests {
  [Fact]
  public void Two_glyphs_on_one_code_share_a_block_number_and_emit_one_entry() {
    // `blockNumbers` is a JSON object keyed by code, so numbers are handed out per code rather than per
    // glyph: numbering per glyph would emit two numbers into one entry and leave cells holding the
    // overwritten number unrequired, and vanilla's InCompleteBlockCount indexes BlockCodes[w] without
    // TryGetValue. Six glyphs, four codes, four numbers.
    var numbers = (JObject)
      Attributes(RoledDef())["multiblockStructure"]!["blockNumbers"]!;

    Assert.Equal(
      """{"exlib:testmega*":1,"exlib:testbrick*":2,"game:air":3,"exlib:testtuyere*":4}""",
      numbers.ToString(Newtonsoft.Json.Formatting.None)
    );

    // Every offset resolves to one of those four, so no cell is orphaned.
    var offsets = (JArray)
      Attributes(RoledDef())["multiblockStructure"]!["offsets"]!;
    Assert.NotEmpty(offsets);
    Assert.All(
      offsets,
      o => Assert.InRange((int)o["w"]!, 1, numbers.Properties().Count())
    );
  }

  [Fact]
  public void One_code_two_glyphs_one_role_answers_only_the_role_glyphs_cells() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();

    // Both `a` and `v` are `game:air`: five cells accept an air block, and the four drawn as `v` are the
    // flue. A role keyed by code, or derived from the block occupying the cell, could not tell them apart.
    Assert.Equal(
      5,
      machine.CellsAccepting(new AssetLocation("game:air")).Count
    );
    Assert.Equal(ExpectedAt(FlueCells, 0), Render(machine.CellsWithRole(Flue)));
    Assert.DoesNotContain(
      Anchor.AddCopy(1, 0, 0), // the `a` cell - air, not flue
      machine.CellsWithRole(Flue)
    );
  }

  [Fact]
  public void Two_glyphs_sharing_a_role_both_contribute_their_cells() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();

    // The T/Y idiom: two glyphs for the two tuyere facings, one role. A role is a set rather than a glyph
    // alias, so both cells come back.
    Assert.Equal(
      ExpectedAt(TuyereCells, 0),
      Render(machine.CellsWithRole(Tuyere))
    );
  }

  [Fact]
  public void A_cell_that_is_two_things_at_once_answers_to_both_roles_at_runtime() {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, OverlappingDef()).Complete();

    // The shipped case is the shaft furnaces' crucible: Chargeable and Pool over the same cells. Both roles
    // answer the same cell, and the two are cached independently rather than one shadowing the other.
    Assert.Equal(
      Render([Anchor.AddCopy(2, 0, 0)]),
      Render(machine.CellsWithRole(Flue))
    );
    Assert.Equal(
      Render(machine.CellsWithRole(Flue)),
      Render(machine.CellsWithRole(Damper))
    );
    Assert.NotSame(machine.CellsWithRole(Flue), machine.CellsWithRole(Damper));

    // The overlap is a property of the glyph rather than of its code: the neighbouring `a` cell is the same
    // game:air and carries neither role.
    Assert.Equal(
      2,
      machine.CellsAccepting(new AssetLocation("game:air")).Count
    );
  }
}
