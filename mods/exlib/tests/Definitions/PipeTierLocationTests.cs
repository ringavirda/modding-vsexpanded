using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The tier axis has to separate two tiers' definitions everywhere the loader keys on, not only in the
/// rendered block code. <see cref="ExDefinitions"/> keys on <see cref="ExBlockDef.Location"/>, and a
/// second def landing on an existing key replaces it silently, so a tier that collides here is deleted
/// from the game with no error, no failing code-level guard and no symptom beyond its absence.
/// <para>
/// Written against the exlib factories rather than the shipping providers, so it holds for any pair of
/// tiers including ones a consumer adds. Two tiers share a domain the moment iiex and iiex merge; while
/// the three tiers are three domains every collision here is latent.
/// </para>
/// </summary>
public class PipeTierLocationTests {
  private const string Domain = "iiex";

  /// <summary>Domains owned by a content mod. A definition exlib itself emits may name
  /// <c>game:</c> or its own <c>exlib:</c>, but naming one of these pins shared library art to one
  /// mod's asset tree - which resolves to nothing the moment that mod is renamed or merged away, and a
  /// blocktype whose shape resolves to nothing loads with no shape rather than failing.</summary>
  private static readonly string[] ContentDomains =
  [
    "iiex",
    "iiex",
    "smex",
    "hpex",
    "iiex",
    "siex",
  ];

  private static List<ExBlockDef> Segments(string tier) =>
    [.. BlockPipe.Segments(Domain, tier)];

  private static List<ExBlockDef> Passthroughs(string tier) =>
    [.. BlockPipePassthrough.Passthroughs(Domain, tier)];

  private static List<ExBlockDef> Tier(string tier) =>
    [.. Segments(tier), .. Passthroughs(tier)];

  [Fact]
  public void Two_tiers_in_one_domain_share_no_definition_location() {
    string[] shared =
    [
      .. Tier(BlockPipe.PlatedTier)
        .Select(d => d.Location.ToString())
        .Intersect(Tier(BlockPipe.CastTier).Select(d => d.Location.ToString()))
        .OrderBy(p => p),
    ];

    Assert.True(
      shared.Length == 0,
      $"{shared.Length} definition location(s) are claimed by both the plated and the cast tier. "
        + "Under one domain the second def loaded replaces the first in ExDefinitions, last-writer-wins "
        + "and unlogged, so one whole tier stops existing while every code-level guard stays green:\n  "
        + string.Join("\n  ", shared)
    );
  }

  [Fact]
  public void Every_tier_declares_the_same_number_of_definitions() {
    // Guards the assertion above against passing because a tier collapsed to nothing rather than
    // because the two are disjoint.
    int plated = Tier(BlockPipe.PlatedTier).Count;

    Assert.True(plated > 0, "the plated tier declares no definitions at all");
    Assert.Equal(plated, Tier(BlockPipe.CastTier).Count);
    Assert.Equal(plated, Tier(BlockPipe.RolledTier).Count);
  }

  [Fact]
  public void Two_tiers_in_one_domain_share_no_segment_shape() {
    // Segments only. The two passthrough blocktypes deliberately share one brick mesh across every
    // tier and differ by the sheet texture alone, so a shared shape path there is the design; the
    // segments are genuinely different art per tier and a shared path would render the wrong mesh.
    string[] shared =
    [
      .. ShapesOf(Segments(BlockPipe.PlatedTier))
        .Intersect(ShapesOf(Segments(BlockPipe.CastTier)))
        .OrderBy(p => p),
    ];

    Assert.True(
      shared.Length == 0,
      $"{shared.Length} segment shape path(s) are claimed by both tiers; under one domain they resolve "
        + "to one art file and the surviving tier renders the other's mesh:\n  "
        + string.Join("\n  ", shared)
    );
  }

  [Fact]
  public void No_pipe_shape_is_pinned_to_a_content_mod_domain() {
    // exlib emits these defs for every tier, so a content domain written here is a cross-assembly
    // literal in the library: it survives only while that exact mod ships that exact asset tree.
    string[] pinned =
    [
      .. new[]
      {
        BlockPipe.PlatedTier,
        BlockPipe.CastTier,
        BlockPipe.RolledTier,
      }
        .SelectMany(t => ShapesOf(Tier(t)))
        .Where(s =>
          ContentDomains.Contains(s.Split(':')[0]) && s.Split(':')[0] != Domain
        )
        .Distinct()
        .OrderBy(s => s),
    ];

    Assert.True(
      pinned.Length == 0,
      $"{pinned.Length} shape path(s) emitted by exlib name a content mod's domain. Shared library art "
        + "belongs in exlib's own tree; pinned like this it resolves to nothing once that mod is "
        + "renamed or merged, and the blocktype then loads with no shape and no error:\n  "
        + string.Join("\n  ", pinned)
    );
  }

  private static IEnumerable<string> ShapesOf(IEnumerable<ExBlockDef> defs) =>
    defs.SelectMany(d =>
        d.ToJson()["shapebytype"] is { } byType
          ? byType
            .Children<JProperty>()
            .Select(p => p.Value["base"]?.ToString() ?? "")
          : []
      )
      .Where(s => s.Length > 0)
      .Distinct();
}
