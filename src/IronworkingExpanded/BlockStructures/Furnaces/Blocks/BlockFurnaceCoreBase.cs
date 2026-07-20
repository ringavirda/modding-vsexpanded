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
  /// The def fragment every furnace core shares: a vanilla refractory-brick grating (the hearth grate
  /// the burden column stands on - a plain cube would be indistinguishable from the forty wall bricks
  /// around it, leaving the player no way to find the anchor while building), oriented by a
  /// <c>side</c> variant and carrying the build-outline projection. The caller adds its own
  /// <c>.Class</c>/<c>.EntityClass</c>, brick-tier texture and <c>.MultiblockLayout</c>.
  /// </summary>
  protected static ExBlockDef Core(string domain, string code, string path) =>
    ExBlockDef
      .Create(domain, code, path)
      // Must precede any other right-click consumer so its PreventSubsequent wins.
      .Behavior("MultiblockStructure")
      .Behavior("HorizontalOrientable")
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .CreativeCommon("*-north")
      .Shape("game:block/clay/grating")
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
