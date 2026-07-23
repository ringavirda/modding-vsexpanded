using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// Shared plumbing for every furnace anchor - the refractory core that sits at the centre of the
/// furnace's lowest layer and carries the whole multiblock. Replaces the old furnace door: the door
/// anchored the structure part-way up the shaft, so a build started in mid-air and a dead furnace
/// could be cleared by swinging one hinge. The core anchors on the bottom layer instead, so a build
/// starts on the ground and clearing a dead furnace means breaking its walls.
/// <para>
/// Orientation is a plain <c>side</c> variant driven by the vanilla <c>HorizontalOrientable</c>
/// behaviour, which is why none of the cores need the door's hand-rolled <c>TryPlaceBlock</c>: there
/// is no <c>BEBehaviorDoor.RotateYRad</c> to seed and no open/closed state to feed the furnace FSM.
/// </para>
/// <para>
/// Only the code, brick tier, block entity and multiblock layout differ between the cold blast
/// furnace, the hot blast furnace and the cupola - everything else is this one fragment, because
/// duplicating the anchor three ways is what broke the furnaces the last time round.
/// </para>
/// </summary>
public abstract class BlockFurnaceCoreBase : Block
{
  /// <summary>
  /// The def fragment every furnace core shares: a refractory-brick cube oriented by a <c>side</c>
  /// variant and carrying the build-outline projection. The caller adds its own
  /// <c>.Class</c>/<c>.EntityClass</c>, brick-tier faces and <c>.MultiblockLayout</c>. The cube's
  /// north face carries an orientation marker and its south face the furnace-type label (both applied
  /// by the caller), so the anchor reads apart from the surrounding wall bricks - and tells the builder
  /// which way it faces - at a glance. That is the job the old visually-distinct grating shape used to
  /// do; the labelled faces do it better, since they also name the furnace.
  /// </summary>
  protected static ExBlockDef Core(
    string domain,
    string code,
    string path,
    params string[] brickTiers
  )
  {
    var def = ExBlockDef
      .Create(domain, code, path)
      // Must precede any other right-click consumer so its PreventSubsequent wins.
      .Behavior("MultiblockStructure")
      .Behavior("HorizontalOrientable");

    // Refractory-tier variant, declared BEFORE the side group so the code reads {code}-{tier}-{side}
    // (and the "*-north" creative/recipe selectors still match). Cores that take any refractory brick
    // pass all three tiers; the tier3-only hot core passes none and stays a single, tier-less block.
    if (brickTiers.Length > 0)
      def = def.VariantGroup("tier", brickTiers);

    return def
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .CreativeCommon("*-north")
      // A plain cube: its per-face texture codes (north/south/east/west/up/down) are what let the
      // caller stamp the orientation marker and type label onto just the two faces that carry them.
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
