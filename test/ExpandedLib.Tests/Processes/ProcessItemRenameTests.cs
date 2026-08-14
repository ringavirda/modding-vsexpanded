using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Renames declared in the stage catalogue. Exlib sees only the current catalogue, so a code that vanished
/// and one that appeared are indistinguishable from a rename without the hint - the contract is declare the
/// old code and get the migration free, never change the convention and we work it out.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class ProcessItemRenameTests {
  private static ProcessRoute Route(string json) {
    Assert.True(
      ProcessRoute.TryParse(
        new JsonObject(JToken.Parse(json)),
        out ProcessRoute? route,
        out string? error
      ),
      error
    );
    return route!;
  }

  private static List<(AssetLocation Old, AssetLocation New)> Remaps(
    string json
  ) => [.. ProcessItemRenames.Remaps([Route(json)])];

  [Fact]
  public void A_declared_former_code_becomes_a_remap_to_the_current_one() {
    var remaps = Remaps(
      """
      {
        "family": "shingledbar",
        "stages": [
          { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod",
            "formerCodes": [ "iiex:wirerod" ] }
        ]
      }
      """
    );

    (AssetLocation old, AssetLocation now) = Assert.Single(remaps);
    Assert.Equal("iiex:wirerod", old.ToString());
    Assert.Equal("iiex:rolledrod", now.ToString());
  }

  [Fact]
  public void Several_former_codes_all_point_at_the_current_one() {
    // A product renamed twice keeps both old names, or the middle one strands the stacks that stopped
    // there.
    var remaps = Remaps(
      """
      {
        "family": "shingledbar",
        "stages": [
          { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod",
            "formerCodes": [ "iiex:wirerod", "iiex:nailrod" ] }
        ]
      }
      """
    );

    Assert.Equal(2, remaps.Count);
    Assert.All(remaps, r => Assert.Equal("iiex:rolledrod", r.New.ToString()));
  }

  [Fact]
  public void A_stage_that_was_never_renamed_contributes_nothing() {
    Assert.Empty(
      Remaps(
        """
        {
          "family": "shingledbar",
          "stages": [ { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod" } ]
        }
        """
      )
    );
  }

  [Fact]
  public void An_opted_out_stage_still_carries_its_rename() {
    // The item is the mod's own rather than generated, but the code still changed, and the stacks in a
    // player's world do not care which of us built it.
    Assert.Single(
      Remaps(
        """
        {
          "family": "shingledbar",
          "stages": [
            { "thickness": 2.0, "acceptedBy": [ "grooved" ], "code": "iiex:rolledrod",
              "generate": false, "formerCodes": [ "iiex:wirerod" ] }
          ]
        }
        """
      )
    );
  }

  [Fact]
  public void A_stage_naming_no_code_cannot_be_renamed_to() {
    Assert.Empty(
      Remaps(
        """
        {
          "family": "shingledbar",
          "stages": [
            { "thickness": 2.0, "acceptedBy": [ "grooved" ], "formerCodes": [ "iiex:wirerod" ] }
          ]
        }
        """
      )
    );
  }
}
