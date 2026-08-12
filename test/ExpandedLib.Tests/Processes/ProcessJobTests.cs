using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Processes;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The terminal registry: one input, one job, one output, and a count. The count is what makes it a shape
/// of its own rather than a one-rung ladder - the shear crops a rod into four rods, so a job that could
/// name only one output could not express the crop table at all.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessJobTests {
  private const string ShearFile = """
    {
      "schema": 1,
      "machine": "shear",
      "jobs": [
        { "input": "iwex:stock-bloom", "stage": 2.0, "family": "grooved",
          "output": "game:rod-iron", "count": 4, "minTorque": 0.3 },
        { "input": "iwex:nailplate", "output": "game:metalnailsandstrips", "count": 4 }
      ]
    }
    """;

  private static List<ProcessJobSet> Parse(
    out List<string> errors,
    params (string Source, string Json)[] files
  ) => ProcessJobLoader.Parse(files, out errors);

  private static ProcessJobSet Shear() =>
    Assert.Single(Parse(out _, ("iwex:shear.json", ShearFile)));

  #region Parsing

  [Fact]
  public void A_job_set_parses_every_field() {
    ProcessJobSet set = Shear();

    Assert.Equal("shear", set.Machine);
    Assert.Equal(1, set.Schema);
    Assert.Equal(2, set.Jobs.Length);

    ProcessJob crop = set.Jobs[0];
    Assert.Equal("iwex:stock-bloom", crop.Input);
    Assert.Equal("game:rod-iron", crop.Output);
    Assert.Equal(4, crop.Count);
    Assert.Equal(2.0f, crop.Stage);
    Assert.Equal("grooved", crop.Family);
    Assert.Equal(0.3f, crop.MinTorque);
  }

  [Fact]
  public void A_job_on_a_whole_item_needs_no_stage() {
    // The nail bench eats a whole nail plate; only the shear crops a piece part way down a ladder.
    ProcessJob whole = Shear().Jobs[1];

    Assert.Null(whole.Stage);
    Assert.Null(whole.Family);
  }

  [Fact]
  public void A_count_defaults_to_one_rather_than_to_nothing() {
    List<ProcessJobSet> sets = Parse(
      out _,
      (
        "mod:x.json",
        """
        { "machine": "shear", "jobs": [ { "input": "a:b", "output": "a:c" } ] }
        """
      )
    );

    Assert.Equal(1, Assert.Single(Assert.Single(sets).Jobs).Count);
  }

  [Fact]
  public void A_set_naming_no_machine_is_rejected() {
    Parse(
      out List<string> errors,
      ("mod:x.json", """{ "jobs": [ { "input": "a:b", "output": "a:c" } ] }""")
    );
    Assert.Contains("machine", Assert.Single(errors));
  }

  [Fact]
  public void A_job_with_no_input_or_no_output_is_rejected() {
    Parse(
      out List<string> noIn,
      (
        "mod:x.json",
        """{ "machine": "shear", "jobs": [ { "output": "a:c" } ] }"""
      )
    );
    Assert.Contains("input", Assert.Single(noIn));

    Parse(
      out List<string> noOut,
      (
        "mod:x.json",
        """{ "machine": "shear", "jobs": [ { "input": "a:b" } ] }"""
      )
    );
    Assert.Contains("output", Assert.Single(noOut));
  }

  [Fact]
  public void A_count_below_one_is_rejected() {
    // A job that yields nothing destroys the player's piece, which no crop table entry ever means.
    Parse(
      out List<string> errors,
      (
        "mod:x.json",
        """
        { "machine": "shear", "jobs": [ { "input": "a:b", "output": "a:c", "count": 0 } ] }
        """
      )
    );
    Assert.Contains("count", Assert.Single(errors));
  }

  [Fact]
  public void A_set_from_a_newer_build_is_refused_rather_than_mis_read() {
    Parse(
      out List<string> errors,
      ("mod:x.json", """{ "schema": 99, "machine": "shear", "jobs": [] }""")
    );
    Assert.Contains("99", Assert.Single(errors));
  }

  #endregion

  #region The registry

  [Fact]
  public void A_machine_finds_the_job_for_what_it_was_given() {
    var registry = new ProcessJobRegistry();
    ProcessJobLoader.Load([("iwex:shear.json", ShearFile)], registry);

    ProcessJob? job = registry.Job("shear", "iwex:nailplate", null, null);
    Assert.Equal("game:metalnailsandstrips", job!.Output);
    Assert.Equal(4, job.Count);
  }

  [Fact]
  public void A_staged_job_matches_only_at_its_own_stage_and_branch() {
    var registry = new ProcessJobRegistry();
    ProcessJobLoader.Load([("iwex:shear.json", ShearFile)], registry);

    Assert.NotNull(registry.Job("shear", "iwex:stock-bloom", 2.0f, "grooved"));
    Assert.Null(registry.Job("shear", "iwex:stock-bloom", 1.5f, "grooved"));
    Assert.Null(registry.Job("shear", "iwex:stock-bloom", 2.0f, "flat"));
  }

  [Fact]
  public void Another_machine_does_not_see_this_one_s_jobs() {
    var registry = new ProcessJobRegistry();
    ProcessJobLoader.Load([("iwex:shear.json", ShearFile)], registry);

    Assert.Null(registry.Job("nailmachine", "iwex:nailplate", null, null));
  }

  [Fact]
  public void Two_mods_add_jobs_to_one_machine() {
    // The whole point of a registry: a mod ships a crop of its own without touching ours.
    var registry = new ProcessJobRegistry();
    List<string> errors = ProcessJobLoader.Load(
      [
        ("iwex:shear.json", ShearFile),
        (
          "othermod:shear-bronze.json",
          """
          {
            "machine": "shear",
            "jobs": [ { "input": "othermod:bronzestrip", "output": "othermod:bronzerivet", "count": 6 } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Empty(errors);
    Assert.Equal(3, registry.Jobs("shear").Count);
    Assert.Equal(
      6,
      registry.Job("shear", "othermod:bronzestrip", null, null)!.Count
    );
  }

  [Fact]
  public void A_second_job_on_one_input_is_reported_and_the_first_stands() {
    // Two crops for one piece is ambiguous, and taking the last writer would make the answer depend on
    // mod load order.
    var registry = new ProcessJobRegistry();
    List<string> errors = ProcessJobLoader.Load(
      [
        ("iwex:shear.json", ShearFile),
        (
          "othermod:hijack.json",
          """
          {
            "machine": "shear",
            "jobs": [ { "input": "iwex:nailplate", "output": "othermod:something", "count": 1 } ]
          }
          """
        ),
      ],
      registry
    );

    Assert.Contains("hijack.json", Assert.Single(errors));
    Assert.Equal(
      "game:metalnailsandstrips",
      registry.Job("shear", "iwex:nailplate", null, null)!.Output
    );
  }

  [Fact]
  public void Loading_replaces_the_registry_rather_than_adding_to_it() {
    var registry = new ProcessJobRegistry();
    var files = new[] { ("iwex:shear.json", ShearFile) };

    ProcessJobLoader.Load(files, registry);
    ProcessJobLoader.Load(files, registry);

    Assert.Equal(2, registry.Jobs("shear").Count);
  }

  [Fact]
  public void An_unknown_machine_has_no_jobs_rather_than_throwing() {
    Assert.Empty(new ProcessJobRegistry().Jobs("nosuchmachine"));
  }

  #endregion
}
