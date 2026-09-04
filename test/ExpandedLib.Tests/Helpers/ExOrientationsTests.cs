using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The declared-scheme rotation rule, one rule for all twelve schemes: rotate each direction letter
/// preserving order; if the result is a declared token use it, otherwise use the unique declared token
/// with the same face set. Neither half works alone - ordered-only breaks the axis and the tee, whose
/// rotations are spelled non-canonically, and set-only cannot tell <c>ns</c> from <c>sn</c>, the
/// difference between an undirected pipe and a directed valve.
/// See docs/design/mechanics/orientation-schemes.md.
/// </summary>
public class ExOrientationsTests {
  #region The worked examples from the design doc

  // docs/design/mechanics/orientation-schemes.md works every scheme at 90 deg (north to west) by
  // hand; these cases are transcribed from it.
  [Theory]
  [InlineData("Face", "n", "w")]
  [InlineData("Axis", "we", "ns")] // ordered gives `sn`, undeclared -> set {s,n} -> `ns`
  [InlineData("DirectedAxis", "we", "sn")] // ordered gives `sn`, declared -> direction survives
  [InlineData("PipeBend", "nw", "ws")] // ordered gives `ws`, declared
  [InlineData("PipeTee", "uwe", "uns")] // ordered gives `usn`, undeclared -> set -> `uns`
  [InlineData("PipeCross", "weud", "nsud")] // ordered gives `snud`, undeclared -> set -> `nsud`
  public void The_documented_rotation_of_every_scheme_at_ninety_degrees(
    string scheme,
    string token,
    string expected
  ) => Assert.Equal(expected, Scheme(scheme).Rotate(token, 90));

  [Fact]
  public void The_ordered_step_is_what_keeps_a_directed_axis_directed() {
    // Both schemes contain `we`; only the directed one also declares `sn`, so only it can preserve
    // the direction. A set-only rule would answer `ns` for both and reverse every valve on rotation.
    Assert.Equal("ns", ExOrientations.Axis.Rotate("we", 90));
    Assert.Equal("sn", ExOrientations.DirectedAxis.Rotate("we", 90));
  }

  [Fact]
  public void The_set_step_is_what_repairs_a_non_canonical_spelling() {
    // The tee's rotations are spelled in whichever order the shape was authored in: `uwe` rotates to
    // `usn`, which the block does not declare, while `uns` (the same three faces) is what it has.
    // Ordered-only would return a token matching no block.
    Assert.DoesNotContain("usn", ExOrientations.PipeTee.Tokens);
    Assert.Contains("uns", ExOrientations.PipeTee.Tokens);
    Assert.Equal("uns", ExOrientations.PipeTee.Rotate("uwe", 90));
  }

  #endregion

  #region Properties that must hold for every scheme

  [Fact]
  public void Four_quarter_turns_return_every_token_to_itself() {
    // Rotation is a group action, so four quarter turns are the identity for every token of every
    // scheme. A mis-spelled fallback entry, or a scheme with two tokens sharing a face set, shows up
    // here as a token that never comes home.
    foreach (ExOrientationScheme scheme in ExOrientations.All)
      foreach (string token in scheme.Tokens) {
        string round = token;
        for (int i = 0; i < 4; i++)
          round = scheme.Rotate(round, 90);
        Assert.Equal($"{scheme.Name}: {token}", $"{scheme.Name}: {round}");
      }
  }

  [Fact]
  public void Every_rotation_of_every_token_is_a_token_the_block_declares() {
    // A rotation must never produce a code the block does not declare: the block lookup then returns
    // null and the caller dereferences it.
    foreach (ExOrientationScheme scheme in ExOrientations.All)
      foreach (string token in scheme.Tokens)
        foreach (int angle in new[] { 0, 90, 180, 270 })
          Assert.True(
            scheme.Contains(scheme.Rotate(token, angle)),
            $"{scheme.Name}.Rotate({token}, {angle}) = "
              + $"'{scheme.Rotate(token, angle)}', which {scheme.Name} does not declare"
          );
  }

  [Fact]
  public void A_rotation_never_changes_how_many_faces_a_token_names() {
    // A bend stays a bend, a tee stays a tee. Catches a fallback reaching across arities, which the
    // set key alone does not prevent since it is only as good as the scheme's table.
    foreach (ExOrientationScheme scheme in ExOrientations.All)
      foreach (string token in scheme.Tokens)
        Assert.Equal(token.Length, scheme.Rotate(token, 90).Length);
  }

  [Fact]
  public void The_set_fallback_is_unambiguous_wherever_it_is_reachable() {
    // The rule's precondition: at most one declared token per face set, or the fallback is ambiguous.
    // The directed schemes break it (`ns` and `sn` share a set) and are exempt because their ordered
    // step always matches first, so the fallback is never reached. Any other duplicate is a defect.
    foreach (ExOrientationScheme scheme in ExOrientations.All) {
      if (scheme.Name.StartsWith("Directed"))
        continue;

      List<string> dupes =
      [
        .. scheme
          .Tokens.GroupBy(t => string.Concat(t.Distinct().OrderBy(c => c)))
          .Where(g => g.Count() > 1)
          .Select(g => string.Join("/", g)),
      ];
      Assert.Equal(
        $"{scheme.Name}: ",
        $"{scheme.Name}: {string.Join(", ", dupes)}"
      );
    }
  }

  [Fact]
  public void A_directed_scheme_reaches_its_ordered_step_for_every_token() {
    // The exemption above holds only if every token of a directed scheme rotates to a token the
    // scheme also declares, which is what makes the ambiguous fallback unreachable for them.
    foreach (
      var scheme in new[]
      {
        ExOrientations.DirectedAxis,
        ExOrientations.DirectedAxisFlat,
      }
    )
      foreach (string token in scheme.Tokens)
        foreach (int angle in new[] { 90, 180, 270 }) {
          string ordered = string.Concat(
            token.Select(c =>
              c is 'u' or 'd'
                ? c
                : ExOrientation.SideFromAngle(
                  ExOrientation.AngleFromSide(c.ToString()) + angle,
                  asLetter: true
                )[0]
            )
          );
          Assert.True(
            scheme.Contains(ordered),
            $"{scheme.Name}: {token}@{angle} -> {ordered} is not declared, "
              + "so the ambiguous set fallback WOULD be reached"
          );
        }
  }

  #endregion

  #region Not-ours tokens, and the vertical no-ops

  [Fact]
  public void A_token_the_scheme_does_not_declare_comes_back_untouched() {
    // Direction letters spell ordinary words, so a material or type segment must never be rotated
    // into a code matching no block.
    foreach (
      string notAToken in new[]
      {
        "sun",
        "used",
        "wend",
        "tier1",
        "refractory",
        "",
      }
    )
      Assert.Equal(notAToken, ExOrientations.Axis.Rotate(notAToken, 90));
  }

  [Fact]
  public void A_vertical_token_is_its_own_image_under_every_y_rotation() {
    // No structure angle moves up or down, which is what RotatesUnderY reports: pinning `ud` in a
    // layout costs a table entry and buys nothing, because the code already matches it literally.
    Assert.Equal("ud", ExOrientations.Axis.Rotate("ud", 90));
    Assert.False(ExOrientations.Axis.RotatesUnderY("ud"));
    Assert.True(ExOrientations.Axis.RotatesUnderY("ns"));

    Assert.Equal("u", ExOrientations.FaceAll.Rotate("u", 270));
    Assert.False(ExOrientations.FaceAll.RotatesUnderY("u"));
    Assert.True(ExOrientations.FaceAll.RotatesUnderY("n"));
  }

  #endregion

  #region Resolving a scheme from a block's declared states

  [Fact]
  public void A_state_list_resolves_to_the_scheme_that_declares_exactly_it() {
    Assert.Same(
      ExOrientations.Face,
      ExOrientations.Resolve(["n", "e", "s", "w"])
    );
    Assert.Same(
      ExOrientations.Axis,
      ExOrientations.Resolve(["ns", "we", "ud"])
    );
    Assert.Same(ExOrientations.AxisFlat, ExOrientations.Resolve(["ns", "we"]));

    // A block may declare its states in any order.
    Assert.Same(
      ExOrientations.Face,
      ExOrientations.Resolve(["w", "n", "s", "e"])
    );
  }

  [Fact]
  public void A_near_miss_resolves_to_nothing_rather_than_to_the_closest_scheme() {
    // Exact set equality. A subset is not the same scheme with fewer states: the set fallback would
    // then map a rotation onto a token the block does not have. Failing to resolve is recoverable,
    // resolving wrongly is not.
    Assert.Null(ExOrientations.Resolve(["n", "e", "s"]));
    Assert.Null(ExOrientations.Resolve(["ns", "we", "ud", "extra"]));
    Assert.Null(ExOrientations.Resolve([]));
    Assert.Null(ExOrientations.Resolve(null));
  }

  [Fact]
  public void The_two_axis_schemes_are_told_apart_by_their_declared_states_alone() {
    // `ns` is a legal member of both the undirected and the directed grammar, so a single token
    // cannot tell a pipe from a valve. The declared state list can.
    Assert.Same(
      ExOrientations.Axis,
      ExOrientations.Resolve(["ns", "we", "ud"])
    );
    Assert.Same(
      ExOrientations.DirectedAxis,
      ExOrientations.Resolve(["ns", "we", "ud", "sn", "ew", "du"])
    );
    Assert.Contains("ns", ExOrientations.Axis.Tokens);
    Assert.Contains("ns", ExOrientations.DirectedAxis.Tokens);
  }

  #endregion

  private static ExOrientationScheme Scheme(string name) =>
    ExOrientations.All.Single(s => s.Name == name);
}
