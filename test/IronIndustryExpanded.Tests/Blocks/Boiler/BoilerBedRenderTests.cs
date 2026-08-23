using System;
using System.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// What the Cornish boiler draws under its barrel. A construction stage owns the coal group as a whole -
/// a finished vessel has a grate and an unfinished one does not - while how much of it is standing is
/// the bed's, so the two compose: an uncharged boiler shows bare bars, and a burning one loses a course
/// at a time. Drawing the stage set as it stands would put a full grate under every finished vessel and
/// leave it there for the whole burn.
/// </summary>
public class BoilerBedRenderTests {
  /// <summary>The element group the art draws the courses under (<c>CoalLayers/L1</c>..<c>L4</c>).</summary>
  private const string Group = "CoalLayers";

  /// <summary>The courses standing in <paramref name="drawn"/>, in the order they are named.</summary>
  private static string[] Courses(string[]? drawn) =>
    [
      .. (drawn ?? []).Where(e =>
        e.StartsWith(Group + "/", StringComparison.Ordinal)
      ),
    ];

  [Fact]
  public void The_construction_stage_raises_the_coal_group_whole() {
    // The premise every case below rests on: taken as it stands, the stage set draws all four courses
    // on an empty grate. If a stage ever stopped naming the group, these tests would pass vacuously.
    Assert.Contains(Group + "/*", BoilerRig.BuiltElements);
  }

  [Theory]
  [InlineData(16, 4)]
  [InlineData(13, 4)]
  [InlineData(12, 3)]
  [InlineData(8, 2)]
  [InlineData(5, 2)]
  [InlineData(1, 1)]
  public void The_drawn_courses_follow_the_charge(int units, int courses) {
    var rig = new BoilerRig().Raise();
    rig.ChargeBed(BoilerRig.DefaultFuel, units);

    Assert.Equal(
      Enumerable.Range(1, courses).Select(i => $"{Group}/L{i}/*"),
      Courses(rig.Be.DrawnElements())
    );
  }

  [Fact]
  public void An_empty_bed_draws_no_courses_at_all() {
    var rig = new BoilerRig().Raise().EmptyBed();

    string[] drawn = rig.Be.DrawnElements()!;

    Assert.Empty(Courses(drawn));
    // The group entry itself is gone too: left in, it draws every course under it and the empty grate
    // would still be full of coal.
    Assert.DoesNotContain(Group, drawn);
    Assert.DoesNotContain(Group + "/*", drawn);
  }

  [Fact]
  public void Burning_the_last_unit_away_clears_the_grate() {
    var rig = new BoilerRig().Raise();
    rig.ChargeBed(BoilerRig.DefaultFuel, units: 1);
    Assert.Single(Courses(rig.Be.DrawnElements()));

    rig.Bed.Consume(1);

    Assert.Empty(Courses(rig.Be.DrawnElements()));
  }

  [Fact]
  public void Everything_else_the_stages_raised_is_left_alone() {
    var rig = new BoilerRig().Raise().EmptyBed();

    string[] expected =
    [
      .. BoilerRig.BuiltElements.Where(e =>
        e != Group && !e.StartsWith(Group + "/", StringComparison.Ordinal)
      ),
    ];

    Assert.Equal(expected, rig.Be.DrawnElements());
  }

  [Fact]
  public void A_grate_no_stage_has_raised_yet_draws_no_coal() {
    // Mid-construction the coal group is not in the stage set, and a charge must not conjure courses
    // into masonry that is not there. Raising the group is the stage's business; filling it is the
    // bed's.
    string[] firstStage = ["MasonryBase/*"];
    var rig = new BoilerRig().Raise(firstStage);

    Assert.Equal(firstStage, rig.Be.DrawnElements());
  }
}
