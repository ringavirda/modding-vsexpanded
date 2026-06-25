using ExpandedLib.Helpers;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The phase-lock math (<see cref="MPAnim.AdvanceFrame"/>) that keeps a driven part - a mixer rotor,
/// an engine piston - turning in step with a mechanical-power axle: a quarter turn of the network
/// advances a quarter of the animation cycle, reversing the axle reverses the part, and the frame
/// accumulates across render frames and wraps within the cycle rather than jumping.
/// </summary>
public class MPAnimTests
{
  private const float TwoPi = GameMath.TWOPI;
  private const float HalfPi = GameMath.PIHALF;

  #region Proportional advance

  [Fact]
  public void Degenerate_animation_stays_at_frame_zero()
  {
    Assert.Equal(0f, MPAnim.AdvanceFrame(7f, 0f, HalfPi, 1));
    Assert.Equal(0f, MPAnim.AdvanceFrame(7f, 0f, HalfPi, 0));
  }

  [Fact]
  public void A_quarter_turn_advances_a_quarter_of_the_cycle()
  {
    // 40-frame cycle, axle turns 90° (a quarter of 2π) -> a quarter of 40 frames.
    Assert.Equal(10f, MPAnim.AdvanceFrame(0f, 0f, HalfPi, 40), 3);
  }

  [Theory]
  [InlineData(20)]
  [InlineData(40)]
  [InlineData(100)]
  public void A_quarter_turn_advances_a_quarter_of_any_cycle_length(int total)
  {
    Assert.Equal(total / 4f, MPAnim.AdvanceFrame(0f, 0f, HalfPi, total), 3);
  }

  #endregion

  #region Direction & accumulation

  [Fact]
  public void Reversing_the_axle_steps_the_frame_backwards_and_wraps()
  {
    // From frame 0, a backward quarter turn wraps to three-quarters of the cycle.
    Assert.Equal(30f, MPAnim.AdvanceFrame(0f, 0f, -HalfPi, 40), 3);
  }

  [Fact]
  public void Frames_accumulate_across_successive_axle_angles()
  {
    // Drive the axle through three quarter-turns; the frame tracks each step and stays in phase.
    float frame = 0f;
    float last = 0f;
    foreach (float angle in new[] { HalfPi, GameMath.PI, GameMath.PI + HalfPi })
    {
      frame = MPAnim.AdvanceFrame(frame, last, angle, 40);
      last = angle;
    }
    Assert.Equal(30f, frame, 3); // three quarter-turns = 3/4 of 40
  }

  [Fact]
  public void A_full_revolution_returns_to_the_same_frame()
  {
    // Four quarter-turns (one whole revolution) wrap back to frame 0.
    float frame = 0f;
    float last = 0f;
    foreach (
      float angle in new[] { HalfPi, GameMath.PI, GameMath.PI + HalfPi, TwoPi - 0.0001f }
    )
    {
      frame = MPAnim.AdvanceFrame(frame, last, angle, 40);
      last = angle;
    }
    // Land just shy of a full turn, so the frame is just shy of wrapping back to 0 (≈ 40).
    Assert.True(frame > 39.9f && frame <= 40f, $"frame was {frame}");
  }

  [Fact]
  public void The_frame_never_leaves_the_cycle_range()
  {
    float frame = 0f;
    float last = 0f;
    // Many forward steps of an odd size: the result must always stay within [0, total).
    for (int i = 1; i <= 50; i++)
    {
      float angle = i * 0.7f;
      frame = MPAnim.AdvanceFrame(frame, last, angle, 24);
      last = angle;
      Assert.InRange(frame, 0f, 24f);
    }
  }

  #endregion

  #region Absolute angle mapping

  [Fact]
  public void FrameFromAngle_maps_the_axle_angle_onto_the_keyframe_span()
  {
    // 60 frames -> span 59 (keyframes 0..59); angle 0 and a full turn both land on frame 0.
    Assert.Equal(0f, MPAnim.FrameFromAngle(0f, 60), 3);
    Assert.Equal(0f, MPAnim.FrameFromAngle(TwoPi, 60), 3); // wraps seamlessly
    Assert.Equal(29.5f, MPAnim.FrameFromAngle(GameMath.PI, 60), 3); // half turn = half the span
    Assert.Equal(59f / 4f, MPAnim.FrameFromAngle(HalfPi, 60), 3); // quarter turn
  }

  [Fact]
  public void FrameFromAngle_is_degenerate_safe()
  {
    Assert.Equal(0f, MPAnim.FrameFromAngle(HalfPi, 1));
    Assert.Equal(0f, MPAnim.FrameFromAngle(HalfPi, 0));
  }

  [Fact]
  public void FrameFromAngle_stays_within_the_span_for_any_angle()
  {
    for (int i = 0; i < 200; i++)
    {
      float angle = i * 0.123f;
      Assert.InRange(MPAnim.FrameFromAngle(angle, 60), 0f, 59f);
    }
  }

  #endregion
}
