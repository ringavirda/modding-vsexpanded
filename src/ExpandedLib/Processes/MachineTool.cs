using System.Collections.Generic;
using ExpandedLib.Definitions;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// The cutting tooling a machine is fitted with: a consumable carrying a hardness tier and nothing else.
/// A job declares the tier it needs (<see cref="ProcessJob.MinTier"/>) and the machine refuses tooling
/// below it, so the tier ladder is the tooling's whole contribution.
/// <para>
/// Distinct from <see cref="ItemDie"/> on purpose. A die names the job, so a bench with no die has no work
/// at all; a tool names only its hardness and the jobs come from the declared table. The shear's blade
/// sets and the machining line's universal cutter are tools; the heading, nail and rivet benches take
/// dies.
/// </para>
/// <para>
/// A tool is recognised by carrying a tier that parses, never by its code, so a third party's tool needs
/// no naming blessing from us. See docs/design/mechanics/machining-line.md.
/// </para>
/// </summary>
public static class MachineTool {
  /// <summary>The attribute key a tool carries its tier under.</summary>
  public const string AttributeKey = "machinetool";

  /// <summary>The schema this parser writes and reads up to.</summary>
  public const int CurrentSchema = SpecSchema.First;

  /// <summary>
  /// Parses and validates a tool's <c>machinetool</c> attribute. Returns false with a human-readable
  /// <paramref name="error"/> on any malformed field, so a bad tool fails at load rather than by the
  /// machine quietly refusing every piece.
  /// </summary>
  public static bool TryParse(JsonObject? node, out int tier, out string? error) {
    tier = 0;
    error = null;

    if (node is not { Exists: true }) {
      error = $"missing '{AttributeKey}' attribute";
      return false;
    }
    if (!SpecSchema.TryRead(node, CurrentSchema, out _, out error))
      return false;

    tier = node["tier"].AsInt(-1);
    if (tier < 0) {
      error =
        "missing or negative 'tier' (a tool with no hardness cannot be checked against a job's floor)";
      return false;
    }
    return true;
  }

  /// <summary>Whether <paramref name="stack"/> is machine tooling, i.e. carries a tier that parses.</summary>
  public static bool IsTool(ItemStack? stack) =>
    TryParse(stack?.Collectible?.Attributes?[AttributeKey], out _, out _);

  /// <summary>
  /// The hardness tier of the tool fitted as <paramref name="stack"/>, or <c>-1</c> when nothing is
  /// fitted or what is fitted is not tooling. Negative rather than zero, because zero is a legitimate
  /// tier that satisfies every ungated job.
  /// </summary>
  public static int TierOf(ItemStack? stack) =>
    TryParse(stack?.Collectible?.Attributes?[AttributeKey], out int tier, out _)
      ? tier
      : -1;

  /// <summary>One tool's tier, as the nested attribute a variant carries. The authoring helper a mod's own
  /// tooling table is written with.</summary>
  public static object Tier(int tier) =>
    new { machinetool = new { schema = CurrentSchema, tier } };

  /// <summary>
  /// Builds a mod's whole tooling itemtype from its own tier table, so a mod that owns a tool owns the
  /// itemtype carrying it - the same extension seam <see cref="ItemDie.Itemtype"/> opens for dies.
  /// </summary>
  /// <param name="domain">The mod's domain; its tools land there.</param>
  /// <param name="code">The itemtype code, e.g. <c>shearblade</c>.</param>
  /// <param name="tiers">Variant name to its tier.</param>
  /// <param name="shape">Shape every variant renders as.</param>
  /// <param name="variantGroup">The group the variants belong to. Defaults to <c>type</c>, but tooling
  /// whose hardness comes from what it was forged from wants <c>metal</c>, so it reads and captures like
  /// every other smithed item.</param>
  public static ExItemDef Itemtype(
    string domain,
    string code,
    IReadOnlyDictionary<string, int> tiers,
    string shape = "game:item/ingot",
    string variantGroup = "type"
  ) {
    var byType = new Dictionary<string, object>();
    foreach ((string type, int tier) in tiers)
      byType["*-" + type] = Tier(tier);

    return ExItemDef
      .Create(domain, code)
      .Shape(shape)
      .VariantGroup(variantGroup, [.. tiers.Keys])
      // Each fitted tool wears independently, so two can never merge into one stack.
      .MaxStackSize(1)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
