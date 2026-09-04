using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// The common surface of a code-first definition - a block (<see cref="ExBlockDef"/>), item
/// (<see cref="ExItemDef"/>) or recipe file (<see cref="ExRecipeDef"/>): the synthetic-asset
/// <see cref="AssetLocation"/> the loader keys on, plus the built JSON payload. Lets
/// <see cref="ExDefinitions"/> discover, register and serialize all three kinds through one generic path.
/// </summary>
public interface IExDef {
  /// <summary>The synthetic asset location the vanilla loader keys on (unique per def).</summary>
  AssetLocation Location { get; }

  /// <summary>The built definition JSON, returned as a defensive clone: a blocktype/itemtype
  /// <c>JObject</c> or a recipe-file array/object.</summary>
  JToken ToJson();
}
