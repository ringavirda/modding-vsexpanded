using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Checks;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="ExlibChecks.All"/> over a hand-built <see cref="ICheckSource"/> proves the seven checks
/// run and report independently: one seeded violation per rule that has one, and nothing for the
/// clean two (<c>DefinitionCatalogue</c>, <c>NetworkNodeContract</c>) or for the clean parts of the
/// domain the seeded violations sit in.
/// </summary>
public class ExlibChecksTests {
  private const string Domain = "stub";

  // The network node every "pinned" and "clean network contract" assertion below is built against:
  // base code + a one-state `type` group + the four-way `orientation` group NetworkOriented() reads,
  // so its concrete codes are "stub:stubnode-normal-{n,e,s,w}".
  private static ExBlockDef Node() =>
    ExBlockDef
      .Create(Domain, "stubnode")
      .VariantGroup("type", "normal")
      .VariantGroup("orientation", "n", "e", "s", "w")
      .NetworkOriented();

  // An ordinary block that stands as a multiblock's principal: one blockNumbers entry pointing at a
  // block nothing registers, and one multiblockFacings entry pinning the node above by its own
  // concrete orientation code - the two failure modes neither the build nor the runtime reports.
  private static ExBlockDef Wall() =>
    ExBlockDef
      .Create(Domain, "stubwall")
      .Attribute(
        "multiblockStructure",
        new JObject {
          ["blockNumbers"] = new JObject {
            ["stub:stubwall"] = 1,
            ["stub:missingblock"] = 2,
          },
        }
      )
      .Attribute(
        "multiblockFacings",
        new JObject { ["stub:stubnode-normal-n"] = new JArray(2) }
      );

  // A base code and a variant of it under a different def entirely - the collision a wildcard built
  // from the shorter code would widen onto.
  private static ExBlockDef Family() => ExBlockDef.Create(Domain, "stubfam");

  private static ExBlockDef FamilyVariant() =>
    ExBlockDef.Create(Domain, "stubfam-big");

  // The concrete codes these four defs actually register - what AssetCheckSource would read off
  // api.World.Blocks, matched by hand here since there is no game to ask.
  private static readonly AssetLocation[] RegisteredCodes =
  [
    new("stub:stubnode-normal-n"),
    new("stub:stubnode-normal-e"),
    new("stub:stubnode-normal-s"),
    new("stub:stubnode-normal-w"),
    new("stub:stubwall"),
    new("stub:stubfam"),
    new("stub:stubfam-big"),
  ];

  // Every registered code gets an English name key except "stubfam" - the one held back to prove
  // LangCoverageCheck.
  private static readonly JObject EnglishLang =
    new(
      RegisteredCodes
        .Where(c => c.Path != "stubfam")
        .Select(c => new JProperty("block-" + c.Path, "Stub Block"))
    );

  // One grid recipe whose output names a block nothing registers. A method rather than a static
  // field: a field typed as a tuple of AssetLocation and JObject forces the CLR to lay out that
  // ValueTuple - and so fully resolve both foreign assemblies - the moment this type loads, which
  // races the harness's own assembly resolver and fails test discovery outright.
  private static (AssetLocation File, JObject Json) DanglingRecipe() => (
    new AssetLocation("stub", "recipes/grid/stubrecipe.json"),
    new JObject {
      ["output"] = new JObject {
        ["type"] = "block",
        ["code"] = "stub:missingrecipeoutput",
      },
    }
  );

  private sealed class StubCheckSource : ICheckSource {
    public IEnumerable<string> Domains => [Domain];
    public IEnumerable<AssetLocation> BlockCodes => RegisteredCodes;
    public IEnumerable<AssetLocation> ItemCodes => [];

    public IEnumerable<(AssetLocation File, JObject Json)> Recipes(string domain) =>
      domain == Domain ? [DanglingRecipe()] : [];

    public IEnumerable<(string Locale, JObject Json)> Lang(string domain) =>
      domain == Domain ? [("en", EnglishLang)] : [];

    public IEnumerable<ExBlockDef> BlockDefinitions(string domain) =>
      domain == Domain ? [Node(), Wall(), Family(), FamilyVariant()] : [];
  }

  private static IReadOnlyList<CheckResult> Results() =>
    ExlibChecks.All(new StubCheckSource());

  private static IReadOnlyList<string> ErrorsOf(string check) =>
    Results().Single(r => r.Check == check).Errors;

  [Fact]
  public void Multiblock_codes_reports_only_the_dangling_cell() {
    IReadOnlyList<string> errors = ErrorsOf("MultiblockCodes");
    Assert.Single(errors);
    Assert.Contains("missingblock", errors[0]);
  }

  [Fact]
  public void Recipe_codes_reports_only_the_dangling_output() {
    IReadOnlyList<string> errors = ErrorsOf("RecipeCodes");
    Assert.Single(errors);
    Assert.Contains("missingrecipeoutput", errors[0]);
  }

  [Fact]
  public void Lang_coverage_reports_only_the_missing_key() {
    IReadOnlyList<string> errors = ErrorsOf("LangCoverage");
    Assert.Single(errors);
    Assert.Contains("block-stubfam", errors[0]);
  }

  [Fact]
  public void Pinned_network_nodes_reports_only_the_pinned_node() {
    IReadOnlyList<string> errors = ErrorsOf("PinnedNetworkNodes");
    Assert.Single(errors);
    Assert.Contains("stubnode-normal-n", errors[0]);
  }

  [Fact]
  public void Code_prefix_collision_reports_only_the_family_pair() {
    IReadOnlyList<string> errors = ErrorsOf("CodePrefixCollision");
    Assert.Single(errors);
    Assert.Contains("stubfam", errors[0]);
    Assert.Contains("stubfam-big", errors[0]);
  }

  [Fact]
  public void Clean_checks_report_nothing() {
    Assert.Empty(ErrorsOf("DefinitionCatalogue"));
    Assert.Empty(ErrorsOf("NetworkNodeContract"));
  }

  [Fact]
  public void All_examines_every_check_for_every_domain_and_nothing_more() {
    IReadOnlyList<CheckResult> results = Results();
    // One CheckResult per (check, domain) pair - seven checks, one domain here.
    Assert.Equal(7, results.Count);
    Assert.All(results, r => Assert.Equal(Domain, r.Domain));
    Assert.Equal(5, results.Sum(r => r.Errors.Count));
  }

  [Fact]
  public void Log_writes_one_summary_line_per_check() {
    ILogger logger = Substitute.For<ILogger>();
    IReadOnlyList<CheckResult> results = Results();

    ExlibChecks.Log(logger, results);

    int summaryLines = logger
      .ReceivedCalls()
      .Count(call =>
        call.GetMethodInfo().Name == nameof(ILogger.Notification)
        && call.GetArguments() is [string fmt, object[] args]
        && fmt == "[exlib] check {0} ({1}): {2} error(s)"
        && args.Length == 3
      );
    Assert.Equal(results.Count, summaryLines);
  }

  [Fact]
  public void AssetCheckSource_over_a_TestWorld_enumerates_its_registered_blocks_and_items() {
    var world = new TestWorld();
    world.Register(TestBlocks.Configure(new Block(), "test:stubblock", 42));
    world.RegisterItem("test:stubitem");

    var source = new AssetCheckSource(world.Api);

    Assert.Contains(source.BlockCodes, c => c.ToString() == "test:stubblock");
    Assert.Contains(source.ItemCodes, c => c.ToString() == "test:stubitem");
  }
}
