using System.Collections.Generic;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Checks;

/// <summary>
/// What a check reads: the codes, files and definitions of one or more domains, either from the
/// game's loaded assets (<see cref="AssetCheckSource"/>) or from a repository source tree
/// (<c>ExpandedLib.Testing.RepoCheckSource</c>). A check never touches a registry, a file system or
/// an assembly on its own - only through this interface - so the same rule runs unmodified in game
/// (<c>AssetsFinalize</c>, <c>/exmod verify</c>) and against the xUnit harness.
/// </summary>
public interface ICheckSource {
  /// <summary>The mod domains this source covers. A reference naming a domain outside this set
  /// belongs to something the source cannot see, and a check must leave it alone rather than guess.</summary>
  IEnumerable<string> Domains { get; }

  /// <summary>Every concrete block code registered across <see cref="Domains"/> - the codes a
  /// reference actually resolves against.</summary>
  IEnumerable<AssetLocation> BlockCodes { get; }

  /// <summary>Every concrete item code registered across <see cref="Domains"/>, the item-side
  /// sibling of <see cref="BlockCodes"/>.</summary>
  IEnumerable<AssetLocation> ItemCodes { get; }

  /// <summary>
  /// Every recipe <paramref name="domain"/> ships, one entry per recipe object rather than per file:
  /// a file holding a JSON array yields one tuple per element, all sharing that file's
  /// <see cref="AssetLocation"/>, so a caller never has to branch on a recipe file's outer shape.
  /// </summary>
  IEnumerable<(AssetLocation File, JObject Json)> Recipes(string domain);

  /// <summary>Every lang file <paramref name="domain"/> ships, as its locale code (e.g. <c>"en"</c>,
  /// taken from the file name) paired with its parsed JSON.</summary>
  IEnumerable<(string Locale, JObject Json)> Lang(string domain);

  /// <summary>Every code-first block definition <paramref name="domain"/> declares. Items and
  /// recipe files are not exposed this way - a check that needs those reads <see cref="Recipes"/> or
  /// the codes directly, since a JSON-only mod has no code-first def to hand back either.</summary>
  IEnumerable<ExBlockDef> BlockDefinitions(string domain);
}
