using System.Linq;
using System.Text;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The injection pipeline that turns registered <see cref="ExBlockDef"/>s into the synthetic
/// <c>blocktypes/</c> assets the object loader reads. Everything up to the <c>AssetManager.Add</c> sink
/// is pure and covered here: the in-memory <see cref="ExSyntheticAsset"/> round-trips its JSON exactly
/// as <c>GetMany&lt;JObject&gt;</c> will read it, and <see cref="ExDefinitions.BuildBlockAssets"/>
/// produces the right location + payload per def. <see cref="ExDefinitions"/> is a process-wide static,
/// so the class is serialized and cleared before each test.
/// </summary>
[Collection("ExDefinitions")]
public class ExDefinitionInjectionTests
{
  public ExDefinitionInjectionTests() => ExDefinitions.Clear();

  #region ExSyntheticAsset
  [Fact]
  public void Synthetic_asset_is_a_real_engine_Asset_the_loader_can_cast()
  {
    // REGRESSION GUARD: the object loader iterates blocktypes as the CONCRETE
    // Vintagestory.Common.Asset (foreach (Asset item in ...)); a custom IAsset throws
    // InvalidCastException and aborts the whole AssetsLoaded phase (server won't start). The injected
    // asset MUST be the real engine type.
    var location = new AssetLocation("iwex", "blocktypes/solidifiediron.json");
    var payload = new JObject { ["code"] = "solidifiediron", ["n"] = 7 };
    IAsset asset = ExSyntheticAsset.Create(
      location,
      Encoding.UTF8.GetBytes(payload.ToString()),
      new ExDefinitionOrigin()
    );

    Assert.Equal("Vintagestory.Common.Asset", asset.GetType().FullName);
    Assert.True(asset.IsLoaded()); // data present -> the manager never calls Origin.LoadAsset
    // ToObject<JObject> is exactly what GetMany<JObject> calls; it must yield the payload back.
    Assert.True(JToken.DeepEquals(payload, asset.ToObject<JObject>()));
  }

  [Fact]
  public void The_back_reference_origin_is_gameplay_allowed_and_serves_nothing()
  {
    // blocktypes is a gameplay-affecting category; an origin returning false would be skipped for it.
    var origin = new ExDefinitionOrigin();
    Assert.True(origin.IsAllowedToAffectGameplay());
    Assert.Empty(origin.GetAssets(AssetCategory.blocktypes));
  }
  #endregion

  #region ExDefinitions registry
  [Fact]
  public void Registering_a_block_makes_it_enumerable()
  {
    var def = ExBlockDef.Create("iwex", "solidifiediron");
    ExDefinitions.RegisterBlock(def);
    Assert.Single(ExDefinitions.Blocks);
    Assert.Same(def, ExDefinitions.Blocks.Single());
  }

  [Fact]
  public void Re_registering_the_same_location_replaces_rather_than_duplicates()
  {
    ExDefinitions.RegisterBlock(ExBlockDef.Create("iwex", "solidifiediron"));
    var replacement = ExBlockDef
      .Create("iwex", "solidifiediron")
      .Resistance(99f);
    ExDefinitions.RegisterBlock(replacement);

    Assert.Single(ExDefinitions.Blocks);
    Assert.Same(replacement, ExDefinitions.Blocks.Single());
  }
  #endregion

  #region BuildBlockAssets (the pipeline up to the Add sink)
  [Fact]
  public void BuildBlockAssets_emits_one_asset_per_def_at_its_location_with_its_json()
  {
    var def = ExBlockDef
      .Create("iwex", "solidifiediron")
      .Material(EnumBlockMaterial.Metal)
      .Resistance(45f);
    ExDefinitions.RegisterBlock(def);

    var built = ExDefinitions.BuildBlockAssets(new ExDefinitionOrigin()).ToList();

    Assert.Single(built);
    var (location, asset) = built[0];
    Assert.Equal(def.Location, location);
    Assert.Equal("iwex", location.Domain);
    Assert.Equal("blocktypes/solidifiediron.json", location.Path);
    // The pipeline produces the concrete engine Asset the loader casts to (not a custom IAsset).
    Assert.Equal("Vintagestory.Common.Asset", asset.GetType().FullName);
    // The asset's parsed JSON is exactly the def's JSON - the loader will build an identical blocktype.
    Assert.True(JToken.DeepEquals(def.ToJson(), asset.ToObject<JObject>()));
  }

  [Fact]
  public void BuildBlockAssets_stamps_the_supplied_origin_on_every_asset()
  {
    ExDefinitions.RegisterBlock(ExBlockDef.Create("iwex", "a"));
    ExDefinitions.RegisterBlock(ExBlockDef.Create("iwex", "b"));

    var origin = new ExDefinitionOrigin();
    var built = ExDefinitions.BuildBlockAssets(origin).ToList();

    Assert.Equal(2, built.Count);
    Assert.All(built, b => Assert.Same(origin, b.asset.Origin));
  }

  [Fact]
  public void BuildBlockAssets_is_empty_when_nothing_is_registered()
  {
    Assert.Empty(ExDefinitions.BuildBlockAssets(new ExDefinitionOrigin()));
  }
  #endregion
}
