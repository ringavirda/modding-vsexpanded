using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PipesAndPowerExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// Pressure valve: a network endpoint that spills its input network's overflow (above the
/// player-set gate pressure) into the output network, or vents to atmosphere when none is plumbed.
/// Right-click raises the gate, sneak+right-click lowers it (see
/// <see cref="BlockEntityPressureValve.GatePressureStep"/>), bounded by the valve's material rating.
/// </summary>
[BlockRegister]
public partial class BlockPressureValve : BlockValve
{
  /// <summary>The pressure-valve blocktype, authored in C# (migrated from pipes/pressurevalve.json).
  /// AllowedOrientations/fallback are derived from it by the base <see cref="BlockPipe"/>.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [PressureValve(domain)];

  private static ExBlockDef PressureValve(string domain) =>
    ExBlockDef
      .Create(domain, "pipe", "pipes/pressurevalve")
      .Class<BlockPressureValve>()
      .EntityClass<BlockEntityPressureValve>()
      .Material(EnumBlockMaterial.Metal)
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone")
      .MaxStackSize(1)
      .CreativeTab("general", "*-pressurevalve-sn-*")
      .CreativeTab("ppex", "*-pressurevalve-sn-*")
      .Behavior("Lockable")
      .VariantGroup("type", "pressurevalve")
      .VariantGroup("orientation", "ns", "we", "ud", "sn", "ew", "du")
      .VariantGroup("material", "iron", "steel")
      .ShapeByType("*-pressurevalve-ns-*", "ppex:pipes/pressurevalve")
      .ShapeByType("*-pressurevalve-we-*", "ppex:pipes/pressurevalve", rotateY: 90)
      .ShapeByType("*-pressurevalve-ud-*", "ppex:pipes/pressurevalve", rotateX: 90)
      .ShapeByType("*-pressurevalve-sn-*", "ppex:pipes/pressurevalve", rotateY: 180)
      .ShapeByType("*-pressurevalve-ew-*", "ppex:pipes/pressurevalve", rotateY: -90)
      .ShapeByType("*-pressurevalve-du-*", "ppex:pipes/pressurevalve", rotateX: -90)
      .TextureByType("*-iron", "iron4", "game:block/metal/sheet-plain/iron4")
      .TextureByType("*-steel", "iron4", "game:block/metal/sheet-plain/steel4")
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 1f)
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  public override bool IsNetworkEndPoint => true;

  // Right-click adjusts the gate pressure instead of the inherited open/close toggle:
  // plain RMB raises it, sneak + RMB lowers it.
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityPressureValve be
    )
      return false;

    // Don't hijack the right-click while the player is placing/using a held item.
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
      .Where(x => x.ActionLangCode != "ppex:blockhelp-valve-toggle")
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "ppex:blockhelp-pressurevalve-increase",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "ppex:blockhelp-pressurevalve-decrease",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
        }
      )
      .ToArray();
}
