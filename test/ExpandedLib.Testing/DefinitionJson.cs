using System;
using System.Text.Json;

namespace ExpandedLib.Testing;

/// <summary>
/// Vanilla pickaxe tool tiers, for pinning a block's <c>requiredMiningTier</c>. Game facts,
/// not mod balance - if vanilla ever renumbers its tool tiers these are what to re-check.
/// </summary>
public static class VanillaToolTiers
{
  public const int Bronze = 3;
  public const int Iron = 4;
  public const int Steel = 5;
}

/// <summary>
/// Shared readers for pinning a code-first definition's emitted JSON in tests (drop/tier/cost
/// guards). The defs emit VS-flavoured JSON (comments, trailing commas), so <see cref="Parse"/>
/// is the one lenient parse every guard goes through; the accessors below read the fields the
/// mega-block guards pin. Used by the smex and hpex drop-tier suites, which used to carry
/// private copies of all three.
/// </summary>
public static class DefinitionJson
{
  /// <summary>Parses def-emitted JSON (comments + trailing commas allowed) into a detached element.</summary>
  public static JsonElement Parse(string json)
  {
    using var doc = JsonDocument.Parse(
      json,
      new JsonDocumentOptions
      {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );
    return doc.RootElement.Clone();
  }

  /// <summary>The block's <c>requiredMiningTier</c> (see <see cref="VanillaToolTiers"/>).</summary>
  public static int MiningTier(JsonElement block) =>
    block.GetProperty("requiredMiningTier").GetInt32();

  /// <summary>
  /// The <c>ExRightClickConstructable</c> entity behavior's properties node (holds
  /// <c>brokenDropsRatio</c> + <c>stages</c>). Throws when the block has no such behavior,
  /// which the calling test surfaces as a failure.
  /// </summary>
  public static JsonElement Constructable(JsonElement block)
  {
    foreach (
      JsonElement b in block.GetProperty("entityBehaviors").EnumerateArray()
    )
    {
      if (
        b.TryGetProperty("name", out JsonElement name)
        && name.GetString() == "ExRightClickConstructable"
      )
        return b.GetProperty("properties");
    }
    throw new InvalidOperationException(
      "block has no ExRightClickConstructable behavior"
    );
  }
}
