using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Parity oracle for exlib's own <c>structurefiller</c> blocktype authored code-first (migrated from
/// blocktypes/structurefiller.json) — the FIRST exlib code-first def, injected via exlib's own
/// <c>DiscoverAndRegister</c> pass. The invisible mega-block filler: hidden from the handbook, a <c>json</c>
/// drawtype over an empty shape, full-cube collision, no drops. Golden is the deleted JSON, minified.
/// </summary>
// Joins the process-wide-registry collection: the discovery test mutates the static ExDefinitions, so it must
// not run in parallel with the injection-pipeline tests (which count the registered defs).
[Collection("ExDefinitions")]
public class StructureFillerDefinitionParityTests
{
  private const string FillerGolden =
    """{"code":"structurefiller","class":"exlib.BlockStructureFiller","entityClass":"exlib.BlockEntityStructureFiller","handbook":{"exclude":true},"blockmaterial":"Metal","shape":{"base":"exlib:block/empty"},"drawtype":"json","sidesolid":{"all":true},"sideopaque":{"all":false},"lightAbsorption":0,"replaceable":500,"resistance":45.0,"drops":[],"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1}}""";

  [Fact]
  public void Filler_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockStructureFiller.Definitions("exlib").Single();
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(FillerGolden), def.ToJson(), out string normalized),
      "structure-filler code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Filler_targets_the_exlib_asset_location_and_hides_from_handbook()
  {
    ExBlockDef def = BlockStructureFiller.Definitions("exlib").Single();
    Assert.Equal("exlib", def.Location.Domain);
    Assert.Equal("blocktypes/structurefiller.json", def.Location.Path);
    Assert.True((bool)def.ToJson()["handbook"]!["exclude"]!);
  }

  [Fact]
  public void Filler_is_discovered_and_registered_under_exlib()
  {
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister("exlib", typeof(BlockStructureFiller).Assembly);
    Assert.Contains(
      ExDefinitions.Blocks,
      d => d is { Code: "structurefiller", Domain: "exlib" }
    );
  }
}
