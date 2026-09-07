using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Checks;

/// <summary>
/// The in-game <see cref="ICheckSource"/>: codes off the live block/item registries, recipes and
/// lang straight from <see cref="ICoreAPI.Assets"/> (post-JSON-patch, whatever a modder actually
/// ships), and code-first block definitions off the process-wide <see cref="ExDefinitions"/>
/// registry. A domain is exlib itself plus every enabled mod that depends on it, JSON-only ones
/// included - <see cref="BlockDefinitions"/> answers empty for one that registers no code-first def,
/// which is not a defect: the checks reading raw assets (<see cref="RecipeCodesCheck"/>,
/// <see cref="LangCoverageCheck"/>) still cover it. A mod with no exlib dependency - vanilla's own
/// "game"/"survival"/"creative" included - is never a domain: its content is not this library's to
/// police, and <c>/exmod verify &lt;domain&gt;</c> is how anyone still wants it named explicitly.
/// </summary>
public sealed class AssetCheckSource(ICoreAPI api) : ICheckSource {
  /// <inheritdoc/>
  // exlib itself plus every enabled mod that names it as a dependency - never a bystander mod (a
  // JSON-only content pack with no exlib dependency) and never vanilla's own "game"/"survival"/
  // "creative", which never declares one.
  public IEnumerable<string> Domains =>
    api
      .ModLoader.Mods.Where(m =>
        m.Info.ModID == "exlib"
        || m.Info.Dependencies.Any(d => d.ModID == "exlib")
      )
      .Select(m => m.Info.ModID)
      .Distinct();

  /// <inheritdoc/>
  public IEnumerable<AssetLocation> BlockCodes =>
    api.World.Blocks.Where(b => b?.Code != null).Select(b => b.Code);

  /// <inheritdoc/>
  public IEnumerable<AssetLocation> ItemCodes =>
    api.World.Items.Where(i => i?.Code != null).Select(i => i.Code);

  /// <inheritdoc/>
  public IEnumerable<(AssetLocation File, JObject Json)> Recipes(string domain) {
    foreach (IAsset asset in api.Assets.GetMany("recipes/", domain))
      foreach (JObject recipe in ReadRecipeObjects(asset))
        yield return (asset.Location, recipe);
  }

  /// <inheritdoc/>
  public IEnumerable<(string Locale, JObject Json)> Lang(string domain) {
    foreach (IAsset asset in api.Assets.GetMany("lang/", domain)) {
      if (TryParseObject(asset, out JObject json))
        yield return (
          Path.GetFileNameWithoutExtension(asset.Location.Path),
          json
        );
    }
  }

  /// <inheritdoc/>
  public IEnumerable<ExBlockDef> BlockDefinitions(string domain) =>
    ExDefinitions.Blocks.Where(d => d.Domain == domain);

  // A recipe file is either one object or a JSON array of them; either way every element sharing
  // that file's location is handed back, so Recipes never makes a caller branch on the outer shape.
  private static IEnumerable<JObject> ReadRecipeObjects(IAsset asset) {
    if (!TryParseToken(asset, out JToken token))
      yield break;

    foreach (JToken item in token is JArray array ? array : [token])
      if (item is JObject obj)
        yield return obj;
  }

  private static bool TryParseObject(IAsset asset, out JObject json) {
    json = null!;
    if (!TryParseToken(asset, out JToken token) || token is not JObject obj)
      return false;
    json = obj;
    return true;
  }

  // Malformed JSON is not this check's job to report - the game's own asset loader already logs a
  // parse failure for a broken file - so a bad file is skipped rather than throwing here.
  private static bool TryParseToken(IAsset asset, out JToken token) {
    token = null!;
    try {
      token = JToken.Parse(asset.ToText());
      return true;
    } catch (Newtonsoft.Json.JsonException) {
      return false;
    }
  }
}
