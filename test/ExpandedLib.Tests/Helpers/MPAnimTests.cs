using ExpandedLib.Helpers;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The phase-lock math (<see cref="MPAnim.AdvanceFrame"/>) that keeps a driven part such as a mixer
/// rotor or engine piston in step with a mechanical-power axle: a quarter turn of the axle advances a
/// quarter of the animation cycle, a reversed axle steps the frame backwards, and the frame
/// accumulates across render frames, wrapping within the cycle.
/// </summary>
public class MPAnimTests {
  private const float TwoPi = GameMath.TWOPI;
  private const float HalfPi = GameMath.PIHALF;

  #region Proportional advance

  [Fact]
  public void Degenerate_animation_stays_at_frame_zero() {
    Assert.Equal(0f, MPAnim.AdvanceFrame(7f, 0f, HalfPi, 1));
    Assert.Equal(0f, MPAnim.AdvanceFrame(7f, 0f, HalfPi, 0));
  }

  [Fact]
  public void A_quarter_turn_advances_a_quarter_of_the_cycle() {
    // 40-frame cycle, axle turns 90° (a quarter of 2π) -> a quarter of 40 frames.
    Assert.Equal(10f, MPAnim.AdvanceFrame(0f, 0f, HalfPi, 40), 3);
  }

  [Theory]
  [InlineData(20)]
  [InlineData(40)]
  [InlineData(100)]
  public void A_quarter_turn_advances_a_quarter_of_any_cycle_length(int total) {
    Assert.Equal(total / 4f, MPAnim.AdvanceFrame(0f, 0f, HalfPi, total), 3);
  }

  #endregion

  #region Direction & accumulation

  [Fact]
  public void Reversing_the_axle_steps_the_frame_backwards_and_wraps() {
    // From frame 0, a backward quarter turn wraps to three-quarters of the cycle.
    Assert.Equal(30f, MPAnim.AdvanceFrame(0f, 0f, -HalfPi, 40), 3);
  }

  [Fact]
  public void Frames_accumulate_across_successive_axle_angles() {
    // Drive the axle through three quarter-turns; the frame tracks each step and stays in phase.
    float frame = 0f;
    float last = 0f;
    foreach (float angle in new[] { HalfPi, GameMath.PI, GameMath.PI + HalfPi }) {
      frame = MPAnim.AdvanceFrame(frame, last, angle, 40);
      last = angle;
    }
    Assert.Equal(30f, frame, 3); // three quarter-turns = 3/4 of 40
  }

  [Fact]
  public void A_full_revolution_returns_to_the_same_frame() {
    // Four quarter-turns (one whole revolution) wrap back to frame 0.
    float frame = 0f;
    float last = 0f;
    foreach (
      float angle in new[]
      {
        HalfPi,
        GameMath.PI,
        GameMath.PI + HalfPi,
        TwoPi - 0.0001f,
      }
    ) {
      frame = MPAnim.AdvanceFrame(frame, last, angle, 40);
      last = angle;
    }
    // Land just shy of a full turn, so the frame is just shy of wrapping back to 0 (≈ 40).
    Assert.True(frame > 39.9f && frame <= 40f, $"frame was {frame}");
  }

  [Fact]
  public void The_frame_never_leaves_the_cycle_range() {
    float frame = 0f;
    float last = 0f;
    // Many forward steps of an odd size: the result must always stay within [0, total).
    for (int i = 1; i <= 50; i++) {
      float angle = i * 0.7f;
      frame = MPAnim.AdvanceFrame(frame, last, angle, 24);
      last = angle;
      Assert.InRange(frame, 0f, 24f);
    }
  }

  #endregion

  #region Absolute angle mapping

  private const int Frames = 60;

  [Fact]
  public void A_full_turn_maps_onto_the_whole_frame_space() {
    // The span is the full frame count, not the last keyframe's number: the animator's live frame
    // space is [0, QuantityFrames), and the stretch from the last keyframe back to the first is an
    // ordinary interpolation segment it renders like any other. Spanning total-1 maps a revolution
    // onto everything BUT that segment, leaving it reachable only by the clip's own advance.
    Assert.Equal(0f, MPAnim.FrameFromAngle(0f, Frames), 4);
    Assert.Equal(Frames / 4f, MPAnim.FrameFromAngle(HalfPi, Frames), 4);
    Assert.Equal(Frames / 2f, MPAnim.FrameFromAngle(GameMath.PI, Frames), 4);
  }

  [Fact]
  public void The_last_frame_is_reachable_so_the_wrap_segment_is_never_skipped() {
    float justShy = MPAnim.FrameFromAngle(TwoPi - 0.001f, Frames);

    Assert.InRange(justShy, Frames - 1f, Frames);
  }

  [Fact]
  public void A_whole_revolution_returns_to_the_first_frame() {
    Assert.Equal(
      MPAnim.FrameFromAngle(0f, Frames),
      MPAnim.FrameFromAngle(TwoPi, Frames),
      4
    );
    Assert.Equal(
      MPAnim.FrameFromAngle(0.4f, Frames),
      MPAnim.FrameFromAngle(0.4f + TwoPi, Frames),
      4
    );
  }

  [Fact]
  public void A_reversed_axle_runs_the_cycle_backwards() {
    // Direction comes from the angle itself - the caller passes the axle's render angle, which
    // decreases when the shaft turns the other way, so there is no baseline to reset and no drift.
    foreach (float angle in new[] { 0.3f, 1.7f, 3.9f, 5.5f }) {
      float forward = MPAnim.FrameFromAngle(angle, Frames);
      float reversed = MPAnim.FrameFromAngle(-angle, Frames);
      Assert.Equal(Frames - forward, reversed, 3);
    }
  }

  [Fact]
  public void FrameFromAngle_is_degenerate_safe() {
    Assert.Equal(0f, MPAnim.FrameFromAngle(HalfPi, 1));
    Assert.Equal(0f, MPAnim.FrameFromAngle(HalfPi, 0));
  }

  [Fact]
  public void FrameFromAngle_stays_within_the_frame_space_for_any_angle() {
    for (int i = -720; i <= 720; i += 7) {
      float frame = MPAnim.FrameFromAngle(i * GameMath.DEG2RAD, Frames);
      Assert.InRange(frame, 0f, Frames);
    }
  }

  #endregion
}
