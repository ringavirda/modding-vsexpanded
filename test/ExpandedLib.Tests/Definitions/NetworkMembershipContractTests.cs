using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shipped-asset guard on declared network memberships. A membership created from a declaration
/// starts with no network type of its own and nothing else can supply one, so a declaration missing
/// <c>networkType</c> leaves the cell outside its run - visible in game only as a network that stopped
/// working. No shipped def declares a membership yet, so the rule is proved here against defs written
/// for the purpose rather than by scanning a corpus that would be empty.
/// </summary>
public class NetworkMembershipContractTests {
  private const string Member = TestWorld.NetworkMemberClass;

  private static readonly Assembly Here =
    typeof(NetworkMembershipContractTests).Assembly;

  #region Definitions under test

  /// <summary>A footprint cell hosting a membership that names its network - the correct shape.</summary>
  private sealed class WellFormedFootprint : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [
        ExBlockDef
          .Create(domain, "contract-goodfootprint")
          .FillerOffsets([
            new FillerCellSpec(
              0,
              0,
              1,
              Behaviors:
              [
                new FillerBehaviorSpec(
                  Member,
                  "north",
                  new { networkType = "pipe" }
                ),
              ]
            ),
          ]),
      ];
  }

  /// <summary>The same cell with the network name left out.</summary>
  private sealed class UntypedFootprint : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [
        ExBlockDef
          .Create(domain, "contract-badfootprint")
          .FillerOffsets([
            new FillerCellSpec(
              0,
              0,
              1,
              Behaviors: [new FillerBehaviorSpec(Member, "north")]
            ),
          ]),
      ];
  }

  /// <summary>A per-type footprint, which lives under <c>attributesByType</c> rather than
  /// <c>attributes</c> - a second table the scan has to read.</summary>
  private sealed class UntypedPerTypeFootprint : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [
        ExBlockDef
          .Create(domain, "contract-badpertype")
          .FillerOffsetsByType(
            "*-large-*",
            [
              new FillerCellSpec(
                1,
                0,
                0,
                Behaviors: [new FillerBehaviorSpec(Member, "east")]
              ),
            ]
          ),
      ];
  }

  /// <summary>A membership declared on the block itself rather than on a footprint cell.</summary>
  private sealed class UntypedEntityBehaviour : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [
        ExBlockDef
          .Create(domain, "contract-badentitybeh")
          .EntityBehavior(Member),
      ];
  }

  #endregion

  private static IReadOnlyList<string> Violations() =>
    NetworkNodeContract.MembershipViolations("exlib", Here);

  [Fact]
  public void The_scan_reaches_every_place_a_membership_can_be_declared() {
    // The premise the rule rests on: a declaration the scan never sees can never be reported, so a
    // rule proved only by its own failures could be passing because it read nothing. Each of the three
    // tables is represented once, and the well-formed def proves a reached declaration can also pass.
    var violations = Violations();

    Assert.Contains(violations, v => v.Contains("contract-badfootprint"));
    Assert.Contains(violations, v => v.Contains("contract-badpertype"));
    Assert.Contains(violations, v => v.Contains("contract-badentitybeh"));
    Assert.DoesNotContain(
      violations,
      v => v.Contains("contract-goodfootprint")
    );
  }

  [Fact]
  public void A_declaration_naming_its_network_is_the_only_thing_that_passes() {
    // Three bad defs and one good one are declared in this file, so the count is the rule and not an
    // accident of what the scan happened to walk.
    Assert.Equal(3, Violations().Count);
  }

  [Fact]
  public void The_violation_names_the_cell_it_was_found_in() {
    string report = Assert.Single(
      Violations(),
      v => v.Contains("contract-badpertype")
    );

    Assert.Contains("fillerOffsets cell (1,0,0)", report);
    Assert.Contains("networkType", report);
  }

  [Fact]
  public void A_non_membership_behaviour_declaration_is_left_alone() {
    // The molten cell and the MP filler port are hosted the same way and carry no networkType; the
    // rule must not widen to every hosted behaviour.
    Assert.DoesNotContain(
      Violations(),
      v => v.Contains("BEBehaviorMoltenCell") || v.Contains("MPFillerPort")
    );
  }

  [Fact]
  public void A_membership_bearing_block_owes_no_type_variant_group() {
    // The rule the placement guard enforces is BlockNetworkNode's machinery end to end, and none of
    // the defs above is one. Widening that guard's candidate filter to reach a membership-bearing cell
    // instead of giving it a rule of its own would report all four here, for a variant group they have
    // no use for.
    var placement = NetworkNodeContract.TypeGroupViolations("exlib", Here);

    Assert.DoesNotContain(placement, v => v.Contains("contract-"));
  }
}
