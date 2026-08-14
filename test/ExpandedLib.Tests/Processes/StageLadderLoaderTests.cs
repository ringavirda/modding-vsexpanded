using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Processes;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Reading the stage catalogue off disk. Ladders live in <c>config/stageladders/</c> rather than on an
/// item, because item generation has to run before the object loader builds items - a ladder carried on an
/// itemtype could not be read in time to generate one. One file per stock family is the convention, but
/// nothing enforces it: the registry merges whatever arrives.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class StageLadderLoaderTests {
  private const string BloomFile = """
    {
      "schema": 1,
      "family": "bloom",
      "stages": [
        { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" },
        { "thickness": 1.0, "acceptedBy": [ "grooved" ] }
      ]
    }
    """;

  private static List<StageLadder> Parse(
    out List<string> errors,
    params (string Source, string Json)[] files
  ) => StageLadderLoader.Parse(files, out errors);

  [Fact]
  public void A_catalogue_file_becomes_a_ladder() {
    List<StageLadder> ladders = Parse(
      out List<string> errors,
      ("iiex:config/stageladders/bloom.json", BloomFile)
    );

    Assert.Empty(errors);
    Assert.Equal("bloom", Assert.Single(ladders).Family);
  }

  [Fact]
  public void A_malformed_file_is_named_and_skipped_rather_than_failing_the_load() {
    // One bad file must not cost every other mod its ladders.
    List<StageLadder> ladders = Parse(
      out List<string> errors,
      ("othermod:config/stageladders/broken.json", """{ "stages": [] }"""),
      ("iiex:config/stageladders/bloom.json", BloomFile)
    );

    Assert.Single(ladders);
    Assert.Contains("broken.json", Assert.Single(errors));
  }

  [Fact]
  public void Text_that_is_not_json_is_reported_rather_than_thrown() {
    Parse(
      out List<string> errors,
      ("mod:config/stageladders/x.json", "not json")
    );
    Assert.Single(errors);
  }

  [Fact]
  public void Loading_replaces_the_registry_rather_than_adding_to_it() {
    // A world reload in the same process must not accumulate a second copy of every rung.
    var registry = new StageLadderRegistry();
    var files = new[] { ("iiex:config/stageladders/bloom.json", BloomFile) };

    StageLadderLoader.Load(files, registry);
    StageLadderLoader.Load(files, registry);

    Assert.Single(registry.Families);
    Assert.Equal(2, registry.Ladder("bloom")!.Stages.Length);
  }

  [Fact]
  public void Two_mods_contributing_one_family_merge_into_one_ladder() {
    var registry = new StageLadderRegistry();

    List<string> errors = StageLadderLoader.Load(
      [
        ("iiex:config/stageladders/bloom.json", BloomFile),
        (
          "othermod:config/stageladders/bloom-serrated.json",
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "acceptedBy": [ "serrated" ], "code": "iiex:rolledrod" } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Empty(errors);
    Assert.Single(registry.Families);
    Assert.Equal(
      ["grooved", "serrated"],
      registry.Ladder("bloom")!.StageAt(2.0f, "serrated")!.AcceptedBy
    );
  }

  [Fact]
  public void A_clash_between_two_mods_is_reported_against_the_file_that_lost() {
    var registry = new StageLadderRegistry();

    List<string> errors = StageLadderLoader.Load(
      [
        ("iiex:config/stageladders/bloom.json", BloomFile),
        (
          "othermod:config/stageladders/hijack.json",
          """
          {
            "family": "bloom",
            "stages": [ { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "othermod:rod" } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Contains("hijack.json", Assert.Single(errors));
    Assert.Equal(
      "iiex:rolledrod",
      registry.Ladder("bloom")!.StageAt(2.0f, "grooved")!.Code
    );
  }

  [Fact]
  public void Every_stage_that_names_a_code_is_reachable_from_the_loaded_set() {
    // What the item emitter reads at inject time: the codes, off the same parse the registry uses.
    List<StageLadder> ladders = Parse(
      out _,
      ("iiex:config/stageladders/bloom.json", BloomFile)
    );

    Assert.Equal(
      ["iiex:rolledrod"],
      ladders
        .SelectMany(l => l.Stages)
        .Where(s => s.IsStoppingPoint)
        .Select(s => s.Code)
    );
  }
}
