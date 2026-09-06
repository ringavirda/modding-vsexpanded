using System.Linq;
using ExpandedLib.Structures;
using Newtonsoft.Json.Linq;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesSerializationTests {
  [Fact]
  public void Roles_serialise_as_the_authored_offsets_of_each_role() {
    // Pinned literally: authored (north-frame) offsets, roles sorted by key, cells in drawing order.
    // "Flue" sorts before "Tuyere" ordinally.
    Assert.Equal(
      JObject
        .Parse(
          """
          {
            "Flue":   [ {"x":2,"y":0,"z":0}, {"x":3,"y":0,"z":0},
                        {"x":2,"y":0,"z":1}, {"x":2,"y":1,"z":0} ],
            "Tuyere": [ {"x":0,"y":0,"z":1}, {"x":1,"y":1,"z":1} ]
          }
          """
        )
        .ToString(Newtonsoft.Json.Formatting.None),
      Attributes(RoledDef())["multiblockRoles"]!.ToString(
        Newtonsoft.Json.Formatting.None
      )
    );
  }

  [Fact]
  public void Roles_ride_in_a_sibling_attribute_and_never_inside_multiblockStructure() {
    // multiblockStructure is deserialised by vanilla's MultiblockStructure and must stay exactly its
    // schema, so roles ride beside it as multiblockFacings does.
    JObject attrs = Attributes(RoledDef());
    Assert.NotNull(attrs["multiblockRoles"]);
    Assert.Null(attrs["multiblockStructure"]!["roles"]);
    Assert.Null(attrs["multiblockStructure"]!["multiblockRoles"]);
  }

  [Fact]
  public void A_serialised_role_table_reads_back_to_the_offsets_it_was_written_from() {
    MultiblockCellRoles roles = MultiblockCellRoles.FromAttributes(
      new Vintagestory.API.Datastructures.JsonObject(Attributes(RoledDef()))
    );

    Assert.False(roles.IsEmpty);
    Assert.Equal(
      FlueCells.Select(c => (c.X, c.Y, c.Z)).OrderBy(t => t).ToList(),
      roles.CellsOf(Flue).OrderBy(t => t).ToList()
    );
    Assert.Equal(
      TuyereCells.Select(c => (c.X, c.Y, c.Z)).OrderBy(t => t).ToList(),
      roles.CellsOf(Tuyere).OrderBy(t => t).ToList()
    );
    // A role the layout never mentions is empty rather than absent, so no caller has to null-check.
    Assert.Empty(roles.CellsOf(MetalTap));
  }
}
