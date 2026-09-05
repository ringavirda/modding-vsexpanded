using System.Linq;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The natural-draught curve, tested as a shape rather than as a table of coefficients. Every number in
/// <c>base + gain·√courses − friction·courses²</c> is a proposal to calibrate in play, so pinning them
/// would fail on every retune while saying nothing about whether the curve still behaves. What must not
/// move is that it rises, peaks and then declines - a stack built past the optimum has to make the
/// furnace worse, or the decline is indistinguishable from a ceiling and the player gets no feedback.
/// </summary>
public class StackDraughtTests {
  #region The curve's shape

  [Fact]
  public void A_bare_flue_pulls_the_base_factor() {
    Assert.Equal(
      IiexValues.BfNaturalDraughtFactor,
      StackDraught.NaturalDraughtFor(0),
      4
    );
  }

  [Fact]
  public void The_curve_rises_to_its_peak_and_declines_after_it() {
    int peak = Peak();
    Assert.True(
      peak > 0,
      $"the curve peaks at {peak} courses - it never rises"
    );

    for (int c = 1; c <= peak; c++)
      Assert.True(
        StackDraught.NaturalDraughtFor(c)
          > StackDraught.NaturalDraughtFor(c - 1),
        $"course {c} does not out-pull course {c - 1}, below the peak at {peak}"
      );

    for (int c = peak + 1; c <= peak + 12; c++)
      Assert.True(
        StackDraught.NaturalDraughtFor(c)
          < StackDraught.NaturalDraughtFor(c - 1),
        $"course {c} out-pulls course {c - 1}, above the peak at {peak}"
      );
  }

  /// <summary>
  /// The decline has to be worth noticing. A curve that peaked and then flattened would let a player
  /// build a chimney twice as tall as it should be and never learn anything from it.
  /// </summary>
  [Fact]
  public void A_stack_built_far_past_the_peak_is_worse_than_no_stack_at_all() {
    Assert.True(
      StackDraught.NaturalDraughtFor(30) < StackDraught.NaturalDraughtFor(0),
      $"30 courses pull {StackDraught.NaturalDraughtFor(30)}, against a bare flue's "
        + $"{StackDraught.NaturalDraughtFor(0)}"
    );
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(9)]
  [InlineData(30)]
  [InlineData(64)]
  public void The_curve_stays_a_usable_air_factor_at_any_height(int courses) {
    float draught = StackDraught.NaturalDraughtFor(courses);

    Assert.True(float.IsFinite(draught), $"{courses} courses gave {draught}");
    Assert.InRange(draught, 0f, 1f);
  }

  /// <summary>A negative count is a caller's arithmetic slip, not a furnace with anti-chimney.</summary>
  [Fact]
  public void A_negative_course_count_reads_as_a_bare_flue() {
    Assert.Equal(
      StackDraught.NaturalDraughtFor(0),
      StackDraught.NaturalDraughtFor(-4),
      4
    );
  }

  #endregion

  #region The two operating inputs

  [Theory]
  [InlineData(0)]
  [InlineData(4)]
  [InlineData(9)]
  public void A_shut_damper_reduces_the_pull(int courses) {
    Assert.True(
      StackDraught.NaturalDraughtFor(courses, damperOpen: false)
        < StackDraught.NaturalDraughtFor(courses, damperOpen: true),
      $"shutting the damper on a {courses}-course stack changed nothing"
    );
  }

  [Theory]
  [InlineData(0)]
  [InlineData(4)]
  [InlineData(9)]
  public void An_open_door_reduces_the_pull(int courses) {
    Assert.True(
      StackDraught.NaturalDraughtFor(courses, venting: true)
        < StackDraught.NaturalDraughtFor(courses, venting: false),
      $"opening a door on a {courses}-course stack changed nothing"
    );
  }

  /// <summary>
  /// Both at once is worse than either alone, and the ordering is the conventional stove reading: a shut
  /// damper is the heavier of the two, being the flue itself rather than a leak beside it.
  /// </summary>
  [Fact]
  public void The_two_inputs_compound() {
    float open = StackDraught.NaturalDraughtFor(6);
    float damped = StackDraught.NaturalDraughtFor(6, damperOpen: false);
    float vented = StackDraught.NaturalDraughtFor(6, venting: true);
    float both = StackDraught.NaturalDraughtFor(
      6,
      damperOpen: false,
      venting: true
    );

    Assert.True(both < damped && both < vented);
    Assert.True(
      damped < vented,
      $"a shut damper ({damped}) must cost more than an open door ({vented})"
    );
    Assert.True(open > vented);
  }

  #endregion

  #region Harness

  /// <summary>
  /// Where the curve turns over, found by walking rather than solved: the peak moves whenever the
  /// coefficients are recalibrated, and the cases above are about the shape either side of it. Computed
  /// here rather than exposed from <c>StackDraught</c>, which has no in-game caller for it - every
  /// reverberatory furnace's chimney is fixed at the height its drawing declares, so no player is
  /// choosing a stack height yet. It moves into production with U9's player-built stacks.
  /// </summary>
  private static int Peak(int searchTo = 64) =>
    Enumerable
      .Range(0, searchTo + 1)
      .OrderByDescending(c => StackDraught.NaturalDraughtFor(c))
      .ThenBy(c => c)
      .First();

  #endregion
}
