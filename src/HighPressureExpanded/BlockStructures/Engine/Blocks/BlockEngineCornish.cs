using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using HighPressureExpanded.BlockStructures.Engine.BlockEntities;
using LowPressureExpanded.BlockStructures.Engine;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HighPressureExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The Cornish engine mega-block (steel, high-pressure tier). Adds the steam control rods: with a
/// wrench, right-click raises the throttle (low→normal→high) and ctrl+right-click lowers it. Ctrl
/// (not sneak) is used because vanilla diverts sneak+right-click to the held item, where the
/// wrench's reverse-rotate would eat it first. The rods answer on the engine's cell and the filler
/// above it; repairs require steel only. All other behavior lives in lpex's <see cref="BlockEngine"/>.
/// </summary>
[BlockRegister]
public partial class BlockEngineCornish
  : BlockEngine,
    IFillerHost,
    IEngineGeometry,
    IExBlockDefProvider
{
  /// <summary>The Cornish engine blocktype, authored in C# (migrated from engine/cornish.json) off the
  /// shared <see cref="BlockEngine.EngineShell"/>, adding only its seven-stage construction table.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Cornish(domain)];

  private static ExBlockDef Cornish(string domain) =>
    EngineShell(
        ExBlockDef
          .Create(domain, "enginecornish", "engine/cornish")
          .Class<BlockEngineCornish>()
          .EntityClass<BlockEntityEngineCornish>(),
        "hpex:engine/cornish"
      )
      // The beam column reserved beside the engine, drawn as a front elevation of the x=0 plane (rows are
      // Y from 3 down to 0, columns are Z 0..2). The bottom row's gaps are the engine cell itself (z=0) and
      // the sub-machine cell (z=2). Authored per-engine because each engine's column differs.
      .FillerOffsets(
        StructureFootprint.Layout(f =>
          f.Origin(0, 3)
            .Slice(
              0,
              """
              # # #
              # # #
              # # #
              O # .
              """
            )
        )
      )
      .Construction(c =>
        c.Stage(s => s.AddElements("Root/Cylinder"))
          .Stage(s =>
            s.RequireMetalPlate(domain, 12)
              .RequireMetalRod(domain, 12)
              .Require("game:burnedbrick-fire", 36)
              .AddElements("Root/BeamSupport")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 16)
              .RequireMetalRod(domain, 8)
              .AddElements("Root/Beam")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireMetalRod(domain, 6)
              .AddElements("Root/Piston")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 6)
              .RequireMetalNails(domain, 6)
              .AddElements("Root/ControlPiston")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 4)
              .RequireMetalRod(domain, 8)
              .AddElements("Root/ControlPistonSteam")
          )
          .Stage(s =>
            s.RequireMetalRod(domain, 8)
              .RequireMetalNails(domain, 8)
              .AddElements("Root/Rod")
          )
      );

  protected override RepairItem[] RepairItems =>
    [
      new(["metalplate-steel"], 4, "steel plate"),
      new(["rod-steel"], 2, "steel rod"),
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    // Direct click on the engine's own cell drives the control rods.
    if (TryThrottle(world, byPlayer, blockSel.Position, blockSel.Position))
      return true;

    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  public override bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  )
  {
    // Forwarded from a footprint filler: the rods only answer on the cell above the engine.
    if (TryThrottle(world, byPlayer, principalSel.Position, clickedCell))
      return true;

    return base.OnFillerInteractStart(
      world,
      byPlayer,
      principalSel,
      clickedCell
    );
  }

  /// <summary>
  /// Adjusts the control rods when a throttle cell is wrench-clicked: right-click raises, ctrl+
  /// right-click lowers. Returns <c>true</c> when consumed. A broken engine answers nothing here,
  /// so the click falls through to the base repair.
  /// </summary>
  private bool TryThrottle(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos enginePos,
    BlockPos clickedCell
  )
  {
    if (!IsThrottleCell(enginePos, clickedCell))
      return false;
    if (
      world.BlockAccessor.GetBlockEntity(enginePos)
        is not BlockEntityEngineCornish be
      || be.IsBroken
    )
      return false;

    ItemStack? held = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
    if (held?.Collectible?.Code?.Path?.Contains("wrench") != true)
      return false;

    if (world.Side == EnumAppSide.Server)
    {
      var player = byPlayer as IServerPlayer;
      int direction = byPlayer.Entity.Controls.CtrlKey ? -1 : 1;
      if (be.AdjustThrottle(direction))
      {
        ExSounds.PlayAt(world, be.Pos, ExSounds.ToggleSwitch, byPlayer);
        player?.SendMessage(
          GlobalConstants.CurrentChatGroup,
          Lang.Get(
            "hpex:engine-throttle-set",
            Lang.Get("hpex:engine-throttle-" + be.ThrottleKey)
          ),
          EnumChatType.Notification
        );
      }
      else
      {
        // Already at the end of the range - tell the player which way it can't go.
        player?.SendIngameError(
          "hpex-engine",
          Lang.Get(
            direction > 0
              ? "hpex:engine-throttle-max"
              : "hpex:engine-throttle-min"
          )
        );
      }
    }
    return true;
  }

  /// <summary>The control rods are reachable on the engine's own cell and the filler directly above it.</summary>
  private static bool IsThrottleCell(
    BlockPos enginePos,
    BlockPos clickedCell
  ) => clickedCell.Equals(enginePos) || clickedCell.Equals(enginePos.UpCopy());

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    WithThrottleHelp(
      world,
      selection.Position,
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
    );

  public override WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  )
  {
    WorldInteraction[] help = base.GetFillerInteractionHelp(
      world,
      principalSel,
      forPlayer,
      clickedCell
    );
    // Only the filler directly above the engine carries the control rods.
    if (!clickedCell.Equals(principalSel.Position.UpCopy()))
      return help;
    return WithThrottleHelp(world, principalSel.Position, help);
  }

  /// <summary>
  /// Appends the throttle raise/lower wrench actions to <paramref name="baseHelp"/> when
  /// the engine at <paramref name="enginePos"/> is constructed and intact (a broken engine
  /// only shows the repair action, which the base already supplies).
  /// </summary>
  private WorldInteraction[] WithThrottleHelp(
    IWorldAccessor world,
    BlockPos enginePos,
    WorldInteraction[] baseHelp
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(enginePos)
        is not BlockEntityEngineCornish be
      || !be.IsConstructed
      || be.IsBroken
    )
      return baseHelp;

    ItemStack[] wrench = ExItems.WrenchStacks(world);
    WorldInteraction raise = new()
    {
      ActionLangCode = "hpex:blockhelp-engine-throttle-up",
      MouseButton = EnumMouseButton.Right,
      Itemstacks = wrench,
    };
    WorldInteraction lower = new()
    {
      ActionLangCode = "hpex:blockhelp-engine-throttle-down",
      MouseButton = EnumMouseButton.Right,
      HotKeyCode = "ctrl",
      Itemstacks = wrench,
    };
    return [.. baseHelp, raise, lower];
  }
}
