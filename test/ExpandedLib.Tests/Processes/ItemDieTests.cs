using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The die carries its job spec, the same idiom as a roll set and a mold pattern: a modder adds a die
/// exactly the way they add a roll set, and the machine reads what to do off the fitted tooling. Settled
/// as E2. See docs/design/mechanics/process-extension.md and machining-line.md.
/// </summary>
public class ItemDieTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  private const string BoltDie = """
    {
      "machinejob": {
        "schema": 1,
        "machine": "heading",
        "jobs": [
          { "input": "game:rod-iron", "output": "iwex:bolt", "count": 4,
            "minTier": 2, "seconds": 3.5, "minTorque": 0.4 }
        ]
      }
    }
    """;

  private static ProcessJobSet Parse(string json) {
    Assert.True(
      ItemDie.TryParse(
        Json(json)[ItemDie.AttributeKey],
        out ProcessJobSet? set,
        out string? error
      ),
      error
    );
    return set!;
  }

  #region The die's own spec

  [Fact]
  public void A_die_carries_the_job_it_does() {
    ProcessJob job = Assert.Single(Parse(BoltDie).Jobs);

    Assert.Equal("heading", Parse(BoltDie).Machine);
    Assert.Equal("game:rod-iron", job.Input);
    Assert.Equal("iwex:bolt", job.Output);
    Assert.Equal(4, job.Count);
  }

  [Fact]
  public void A_die_carries_what_the_job_costs() {
    // A machine tool's job is not free: it needs a tempered tool, drive, and time. All three are the
    // die's to declare, so the machine names no number of its own either.
    ProcessJob job = Assert.Single(Parse(BoltDie).Jobs);

    Assert.Equal(2, job.MinTier);
    Assert.Equal(3.5f, job.Seconds);
    Assert.Equal(0.4f, job.MinTorque);
  }

  [Fact]
  public void The_costs_default_so_a_sparse_die_still_works() {
    ProcessJob job = Assert.Single(
      Parse(
        """
        {
          "machinejob": {
            "machine": "nail",
            "jobs": [ { "input": "iwex:nailplate", "output": "game:metalnailsandstrips", "count": 4 } ]
          }
        }
        """
      ).Jobs
    );

    Assert.Equal(0, job.MinTier);
    Assert.Equal(0f, job.MinTorque);
    Assert.True(
      job.Seconds > 0f,
      "a job that takes no time would complete every tick"
    );
  }

  [Fact]
  public void A_die_that_is_not_a_die_is_reported_rather_than_throwing() {
    Assert.False(ItemDie.TryParse(null, out _, out string? error));
    Assert.Contains(ItemDie.AttributeKey, error!);
  }

  [Fact]
  public void A_die_is_refused_by_the_same_rules_a_declared_table_meets() {
    Assert.False(
      ItemDie.TryParse(
        Json(BoltDie.Replace("\"count\": 4", "\"count\": 0"))[
          ItemDie.AttributeKey
        ],
        out _,
        out string? error
      )
    );
    Assert.Contains("count", error!);
  }

  #endregion

  #region Reading it off a stack

  [Fact]
  public void The_job_for_a_piece_comes_off_the_fitted_die() {
    // What a machine does at its tool slot: read the fitted die, ask it for the job matching the piece.
    var die = new Item {
      Code = new AssetLocation("iwex", "die-bolt"),
      Attributes = Json(BoltDie),
    };

    ProcessJob? job = ItemDie.JobFor(
      new ItemStack(die),
      "game:rod-iron",
      null,
      null
    );

    Assert.Equal("iwex:bolt", job!.Output);
  }

  [Fact]
  public void A_die_that_does_not_take_this_piece_offers_no_job() {
    var die = new Item {
      Code = new AssetLocation("iwex", "die-bolt"),
      Attributes = Json(BoltDie),
    };

    Assert.Null(
      ItemDie.JobFor(new ItemStack(die), "iwex:nailplate", null, null)
    );
  }

  [Fact]
  public void A_stack_that_is_not_a_die_offers_no_job() {
    Assert.Null(ItemDie.JobFor(null, "game:rod-iron", null, null));
    Assert.False(ItemDie.IsDie(null));
  }

  [Fact]
  public void A_die_is_recognised_by_carrying_a_job_and_nothing_else() {
    // The same test the mill uses for a roll set: tooling is what parses, not what is named a certain way,
    // so a third party's die needs no code-name blessing from us.
    var die = new Item {
      Code = new AssetLocation("othermod", "whatever"),
      Attributes = Json(BoltDie),
    };

    Assert.True(ItemDie.IsDie(new ItemStack(die)));
  }

  #endregion

  #region The authoring seam

  [Fact]
  public void A_mod_builds_its_whole_die_itemtype_from_its_own_job_table() {
    // The factory is the seam, not the table: PatternItemDefinitions.Itemtype is the proven shape and
    // this follows it, so a mod owning a die owns the itemtype that carries it.
    ExItemDef def = ItemDie.Itemtype(
      "othermod",
      new Dictionary<string, object> {
        ["bolt"] = ItemDie.Job("heading", "game:rod-iron", "othermod:bolt", 4),
        ["rivet"] = ItemDie.Job(
          "heading",
          "game:rod-iron",
          "othermod:rivet",
          6
        ),
      }
    );

    JObject json = def.ToJson();
    Assert.Equal("die", json["code"]!.ToString());
    Assert.Equal("othermod", def.Location.Domain);
    Assert.Equal(
      ["bolt", "rivet"],
      json["variantgroups"]![0]!["states"]!.Select(s => s.ToString())
    );

    // Each variant's spec parses back through the same reader the machine uses.
    var byType = (JObject)json["attributesByType"]!;
    Assert.True(
      ItemDie.TryParse(
        new JsonObject(byType["*-bolt"]!)[ItemDie.AttributeKey],
        out ProcessJobSet? set,
        out string? error
      ),
      error
    );
    Assert.Equal("othermod:bolt", Assert.Single(set!.Jobs).Output);
  }

  #endregion
}
