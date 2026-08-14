using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// Shared plumbing for every furnace anchor: the refractory core at the centre of the furnace's lowest
/// layer, which carries the whole multiblock. Anchoring on the bottom layer means a build starts on the
/// ground and a dead furnace is cleared by breaking its walls. Orientation is a plain <c>side</c> variant
/// driven by the vanilla <c>HorizontalOrientable</c> behaviour, so no core needs its own
/// <c>TryPlaceBlock</c>. The cold blast furnace, hot blast furnace and cupola differ only in code, brick
/// tier, block entity and multiblock layout.
/// </summary>
public abstract class BlockFurnaceCoreBase : Block {
  /// <summary>
  /// The single <c>code</c> every furnace part is registered under - cores, doors, hearths, chimney cap,
  /// charge pile, tap, tuyere and blower - each told apart by its <c>type</c> variant. A part with its own
  /// <c>furnace-X</c> code would have this code as a proper prefix, so a <c>Code + "*"</c> selector fed
  /// into a multiblock <c>Legend</c> would match the wrong part while every code still resolved. Guarded
  /// by <c>CodePrefixCollision</c>; rule N7 in docs/design/mechanics/naming.md.
  /// </summary>
  /// <remarks>A family wildcard must be the generated <c>.Any</c> (<c>iiex:furnace-tuyere-*</c>), never
  /// <c>Code + "*"</c>. The guard compares code against code and cannot see that misuse.</remarks>
  public const string FurnaceCode = "furnace";

  /// <summary>
  /// The def fragment every furnace core shares: a refractory-brick cube oriented by a <c>side</c> variant
  /// and carrying the build-outline projection. The caller adds its own <c>.Class</c>/<c>.EntityClass</c>,
  /// brick-tier face textures and <c>.MultiblockLayout</c>, and stamps an orientation marker on the north
  /// face and the furnace-type label on the south so the anchor reads apart from the surrounding wall
  /// bricks.
  /// </summary>
  /// <param name="code">The blocktype <c>code</c>. iiex's cores pass <see cref="FurnaceCode"/>; siex
  /// passes its own, since a shared helper must not rename another mod's block.</param>
  /// <param name="type">The <c>type</c> state naming the family member, or <c>null</c> for a core that
  /// carries no <c>type</c> group at all.</param>
  protected static ExBlockDef Core(
    string domain,
    string code,
    string? type,
    string path,
    params string[] brickTiers
  ) {
    var def = ExBlockDef
      .Create(domain, code, path)
      // Must precede any other right-click consumer so its PreventSubsequent wins.
      .Behavior("MultiblockStructure")
      .Behavior("ExOrientable");

    // Declared first so the code reads furnace-{type}-{tier}-{side}. See N7 in
    // docs/design/mechanics/naming.md. A null type leaves the block without a type group, which is what
    // siex passes to keep its own code unchanged.
    if (type != null)
      def = def.VariantGroup("type", type);

    // Refractory-tier variant, declared before the side group so the code reads {code}-{tier}-{side} and
    // the "*-n" creative and recipe selectors still match. Cores that accept any refractory brick pass all
    // three tiers; a core fixed to one tier passes none and stays a single, tier-less block.
    if (brickTiers.Length > 0)
      def = def.VariantGroup("tier", brickTiers);

    return def.SideVariant()
      .CreativeCommon("*-n")
      // A plain cube. Its per-face texture codes (north/south/east/west/up/down) let the caller stamp the
      // orientation marker and type label onto single faces.
      .Shape("game:block/basic/cube")
      .Material(EnumBlockMaterial.Ceramic)
      .MaxStackSize(4)
      .Replaceable(700)
      .Resistance(2.5f)
      .LightAbsorption(99)
      .SolidNonOpaque()
      .Sound("walk", "game:walk/stone")
      .Sound("place", "game:block/ceramicplace")
      .MaterialDensity(2000);
  }
}
