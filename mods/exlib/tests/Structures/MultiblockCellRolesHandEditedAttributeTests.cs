using ExpandedLib.Structures;
using Newtonsoft.Json.Linq;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesHandEditedAttributeTests {
  /// <summary>
  /// Reads a raw <c>multiblockRoles</c> body as a hand-edited JSON patch would present it. That is the only
  /// route by which a malformed table reaches the reader, since the builder rejects these cases at authoring
  /// time.
  /// </summary>
  private static MultiblockCellRoles ReadRoles(string rolesBody) =>
    MultiblockCellRoles.FromAttributes(
      new Vintagestory.API.Datastructures.JsonObject(
        JObject.Parse("{\"multiblockRoles\":" + rolesBody + "}")
      )
    );

  [Fact]
  public void A_cell_whose_coordinate_is_not_an_int_is_skipped_rather_than_thrown_on() {
    // The read happens in SetStructureAngle, i.e. on the server monitor tick and in a client GetBlockInfo,
    // so a throw there repeats on a live block entity mid-session. Newtonsoft's (int) cast throws
    // FormatException on a string and ArgumentException on an object, a bool and a null.
    MultiblockCellRoles roles = ReadRoles(
      """
      {
        "Flue": [ {"x":"two","y":0,"z":0},
                  {"x":{"nested":1},"y":0,"z":0},
                  {"x":true,"y":0,"z":0},
                  {"x":null,"y":0,"z":0},
                  {"x":1.5,"y":0,"z":0},
                  {"x":9999999999999,"y":0,"z":0},
                  {"y":0,"z":0},
                  {"x":2,"y":0,"z":1} ]
      }
      """
    );

    // Seven unreadable cells dropped, the one well-formed cell kept.
    Assert.Equal((2, 0, 1), Assert.Single(roles.CellsOf(Flue)));
  }

  [Fact]
  public void A_role_whose_every_cell_is_malformed_reads_as_no_roles_at_all() {
    // Nothing readable means no roles at all, not a role present but empty.
    Assert.True(
      ReadRoles("""{ "Flue": [ {"x":"two","y":0,"z":0} ] }""").IsEmpty
    );
  }

  [Fact]
  public void Any_non_blank_key_reads_as_a_role() {
    // CellRole is an open string key, so "99" and "Nonsense" are both roles in their own right rather
    // than something to reject - see A_third_party_role_needs_nothing_from_exlib for the same point made
    // through the layout builder rather than raw JSON.
    Assert.NotEmpty(
      ReadRoles("""{ "99": [ {"x":0,"y":0,"z":0} ] }""")
        .CellsOf(CellRole.Of("99"))
    );
    Assert.NotEmpty(
      ReadRoles("""{ "Nonsense": [ {"x":0,"y":0,"z":0} ] }""")
        .CellsOf(CellRole.Of("Nonsense"))
    );

    // A key is read back exactly, not case-insensitively: "flue" and "Flue" are different roles now
    // that neither is a spelling exlib itself owns.
    Assert.Empty(
      ReadRoles("""{ "flue": [ {"x":0,"y":0,"z":0} ] }""").CellsOf(Flue)
    );
  }
}
