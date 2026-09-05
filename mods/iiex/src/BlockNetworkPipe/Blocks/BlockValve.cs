using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// Manually-toggled in-line valve on a pipe run. Open, it is a normal pipe node and the run flows
/// through; closed, it severs the run at its cell (see
/// <see cref="BlockEntityValve.IsConnectionBroken"/>). Empty-hand right-click toggles it.
/// </summary>
[BlockRegister]
public partial class BlockValve : BlockPipe {
  /// <summary>The in-line valve blocktype. AllowedOrientations and the fallback are derived from it
  /// by the base <see cref="BlockPipe"/>.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Valve(domain)];

  private static ExBlockDef Valve(string domain) =>
    ExBlockDef
      .Create(domain, "pipe", "pipe/valve")
      .Class<BlockValve>()
      .EntityClass<BlockEntityValve>()
      .EntityBehavior("Animatable")
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(1)
      .CreativeTab("general", "*-valve-sn")
      .CreativeTab("iiex", "*-valve-sn")
      .Behavior("Lockable")
      // Cast-tier, and declared first like the segments': the valve is flanged and rated against the
      // cast main it sits in, which nothing on the block said once the tier stopped being the domain.
      .VariantGroup("tier", BlockPipe.CastTier)
      .VariantGroup("type", "valve")
      .VariantGroup("orientation", "ns", "we", "ud", "sn", "ew", "du")
      .NetworkOriented()
      .ShapeByType("*-valve-ns", "iiex:pipe/valve")
      .ShapeByType("*-valve-we", "iiex:pipe/valve", rotateY: 90)
      .ShapeByType("*-valve-ud", "iiex:pipe/valve", rotateX: 90)
      .ShapeByType("*-valve-sn", "iiex:pipe/valve", rotateY: 180)
      .ShapeByType("*-valve-ew", "iiex:pipe/valve", rotateY: -90)
      .ShapeByType("*-valve-du", "iiex:pipe/valve", rotateX: 90, rotateY: 180)
      .Texture("iron4", "game:block/metal/sheet-plain/iron4")
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityValve be
    ) {
      // The right-click stays with the held item while the player has one.
      if (!byPlayer.Entity.RightHandItemSlot.Empty)
        return false;

      // ToggleOpen re-walks the network so the connectivity change applies immediately.
      if (world.Side == EnumAppSide.Server)
        be.ToggleOpen();

      ExSounds.PlayAt(
        world,
        blockSel.Position,
        ExSounds.CokeOvenDoorOpen,
        byPlayer
      );

      return true;
    }
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var toggleHelp = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-valve-toggle",
      MouseButton = EnumMouseButton.Right,
    };

    return baseHelp.Append(toggleHelp).ToArray();
  }
}
