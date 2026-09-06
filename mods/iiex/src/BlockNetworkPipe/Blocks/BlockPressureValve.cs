using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// Pressure valve: a network endpoint that spills its input network's overflow (above the
/// player-set gate pressure) into the output network, or vents to atmosphere when none is plumbed.
/// Right-click raises the gate, sneak+right-click lowers it (see
/// <see cref="BlockEntityPressureValve.GatePressureStep"/>), bounded by the valve's material rating.
/// </summary>
[BlockRegister]
public partial class BlockPressureValve : BlockValve {
  /// <summary>The pressure-valve blocktype. AllowedOrientations and the fallback are derived from it
  /// by the base <see cref="BlockPipe"/>.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [PressureValve(domain)];

  private static ExBlockDef PressureValve(string domain) =>
    ExBlockDef
      .Create(domain, "pipe", "pipe/pressurevalve")
      .Class<BlockPressureValve>()
      .EntityClass<BlockEntityPressureValve>()
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(1)
      .CreativeTab("general", "*-pressurevalve-sn")
      .CreativeTab("iiex", "*-pressurevalve-sn")
      .Behavior("Lockable")
      // Cast-tier, as BlockValve. This is where MaxGatePressure's ceiling comes from: the valve never
      // bursts itself, so its gate is capped by the rating of the tier it is made of.
      .VariantGroup("tier", BlockPipe.CastTier)
      .VariantGroup("type", "pressurevalve")
      .VariantGroup("orientation", "ns", "we", "ud", "sn", "ew", "du")
      .NetworkOriented()
      .ShapeByType("*-pressurevalve-ns", "iiex:pipe/pressurevalve")
      .ShapeByType("*-pressurevalve-we", "iiex:pipe/pressurevalve", rotateY: 90)
      .ShapeByType("*-pressurevalve-ud", "iiex:pipe/pressurevalve", rotateX: 90)
      .ShapeByType(
        "*-pressurevalve-sn",
        "iiex:pipe/pressurevalve",
        rotateY: 180
      )
      .ShapeByType(
        "*-pressurevalve-ew",
        "iiex:pipe/pressurevalve",
        rotateY: -90
      )
      .ShapeByType(
        "*-pressurevalve-du",
        "iiex:pipe/pressurevalve",
        rotateX: -90
      )
      .Texture("iron4", "game:block/metal/sheet-plain/iron4")
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  public override bool IsNetworkEndPoint => true;

  // Adjusts the gate pressure instead of the inherited open/close toggle.
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityPressureValve be
    )
      return false;

    // The right-click stays with the held item while the player has one.
    if (!byPlayer.Entity.RightHandItemSlot.Empty)
      return false;

    if (world.Side == EnumAppSide.Client)
      return true;

    bool increase = !byPlayer.Entity.Controls.ShiftKey;
    if (be.AdjustGatePressure(increase))
      ExSounds.PlayAt(
        world,
        blockSel.Position,
        ExSounds.ToggleSwitch,
        byPlayer
      );

    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
      .Where(x => x.ActionLangCode != "iiex:blockhelp-valve-toggle")
      .Append(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-pressurevalve-increase",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .Append(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-pressurevalve-decrease",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
        }
      )
      .ToArray();
}
