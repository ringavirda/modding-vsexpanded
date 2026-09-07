using System;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using Newtonsoft.Json.Linq;
using Xunit;
using static ExpandedLib.Tests.MultiblockCellRolesFixtures;

namespace ExpandedLib.Tests;

public class MultiblockCellRolesRoleArityTests {
  [Fact]
  public void Exactly_the_two_taps_are_single_cell_roles() {
    // The arity split, pinned so that adding a role forces an explicit answer rather than inheriting the
    // set default by omission. Every other role is a genuine set: a shaft is a column, a hearth has two
    // tuyeres, and a stack throttled at both ends has two dampers. MetalTap/SlagTap are declared
    // single-cell above, at the point CellRole.Of mints them.
    Assert.Equal(
      new[] { MetalTap, SlagTap },
      new CellRole[]
      {
        Chargeable,
        Firebox,
        Tuyere,
        MetalTap,
        SlagTap,
        Flue,
        Damper,
      }
        .Where(CellRoles.IsSingleCell)
        .ToArray()
    );
  }

  [Fact]
  public void A_single_cell_role_drawn_twice_fails_the_build() {
    // `MetalTapCell` and `SlagTapCell` are a single Vec3i, so callers read CellsWithRole(MetalTap).Single().
    // Without the build-time check that is an InvalidOperationException out of LINQ on a live block entity.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('m', "game:air")
            .Role('m', MetalTap)
            .Layer(0, "C m m")
        )
    );
    Assert.Contains("draws 2 cells", ex.Message, StringComparison.Ordinal);
    Assert.Contains("MetalTap", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void Two_glyphs_cannot_share_a_single_cell_role() {
    // Counted over the drawn cells rather than over the role table, so the two-glyph route that the T/Y
    // idiom makes natural fails the same way as one glyph drawn twice.
    Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('m', "game:air")
            .Legend('n', "game:air")
            .Role('m', SlagTap)
            .Role('n', SlagTap)
            .Layer(0, "C m n")
        )
    );
  }

  [Fact]
  public void A_single_cell_role_drawn_once_builds_and_a_set_role_carries_many() {
    // Both directions in one layout: one tap, four flue cells. Without the accepting half, a guard that
    // rejected every single-cell role, or every role, would look correct.
    var roles = (JObject)
      Attributes(
        ExBlockDef
          .Create("exlib", "testmega")
          .MultiblockLayout(s =>
            s.Legend('C', "exlib:testmega*")
              .Legend('m', "game:air")
              .Legend('v', "game:air")
              .Role('m', MetalTap)
              .Role('v', Flue)
              .Layer(0, "C m v v v v")
          )
      )["multiblockRoles"]!;

    Assert.Single((JArray)roles["MetalTap"]!);
    Assert.Equal(4, ((JArray)roles["Flue"]!).Count);
  }
}
