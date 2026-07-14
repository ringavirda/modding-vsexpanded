using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SteelmakingExpanded.BlockStructures.Converter.Blocks;

/// <summary>
/// The bessemer control block - the operator interface and "brain" anchor of the
/// converter multiblock. Routes player input to the <see cref="BlockEntityConverterControl"/>:
/// inspecting the structure while building, and selecting the Normal/Filling/Pouring
/// state once complete.
/// </summary>
[BlockRegister]
public partial class BlockConverterControl : Block, IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The converter-control blocktype, authored in C# (migrated from converter/control.json). The
  /// <c>multiblockStructure</c> table is authored via <see cref="MultiblockBuilder"/> - named block numbers,
  /// a <c>Fill</c>ed body, bespoke tap/canal cells, and build-time validation that every offset resolves to
  /// a declared number with no duplicate cell (the data-table win for a machine with a structure map).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Control(domain)];

  private static ExBlockDef Control(string domain) =>
    ExBlockDef
      .Create(domain, "convertercontrol", "converter/control")
      .Class<BlockConverterControl>()
      .EntityClass<BlockEntityConverterControl>()
      .EntityBehavior("Animatable")
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(1)
      .CreativeTab("general", "*-north")
      .CreativeTab("smex", "*-north")
      .Multiblock(m =>
        m.Number("smex:convertercontrol*", 1)
          .Number("smex:convertertransmission*", 2)
          .Number("smex:converterbessemer*", 3)
          .Number("smex:converter-intake*", 4)
          .Number("iwex:moltencanal-tap*", 5)
          .Number("iwex:moltencanal-start*", 6)
          .Number("iwex:moltencanal-straight*", 7)
          .Number("exlib:structurefiller", 8)
          // control + transmission below it
          .At(0, 0, 0, 1)
          .At(0, -1, 0, 2)
          // z=1 body (all filler)
          .Fill(-1, -1, 1, 1, 1, 1, 8)
          // z=2 body: filler, with the bessemer at the centre and the tap cell (1,1,2) left for below
          .At(-1, -1, 2, 8)
          .At(0, -1, 2, 8)
          .At(1, -1, 2, 8)
          .At(-1, 0, 2, 8)
          .At(0, 0, 2, 3)
          .At(1, 0, 2, 8)
          .At(-1, 1, 2, 8)
          .At(0, 1, 2, 8)
          // z=3 body (all filler)
          .Fill(-1, -1, 3, 1, 1, 3, 8)
          // intake spout, then the tap + canal run
          .At(0, 0, 4, 4)
          .At(1, 1, 2, 5)
          .At(2, 1, 2, 7)
          .At(1, -2, 2, 6)
          .At(2, -2, 2, 7)
      )
      .Behavior("MultiblockStructure")
      .Behavior("HorizontalOrientable")
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .ShapeByType("*-north", "smex:converter/control", rotateY: 0)
      .ShapeByType("*-east", "smex:converter/control", rotateY: 270)
      .ShapeByType("*-south", "smex:converter/control", rotateY: 180)
      .ShapeByType("*-west", "smex:converter/control", rotateY: 90)
      .SideSolid(false)
      .SideOpaque(false);

  #endregion

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityConverterControl be
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    var controls = byPlayer.Entity.Controls;

    // Ctrl+Shift shows the structure projection while still building (handled by the
    // shared MultiblockStructure behavior - defer to base so it receives the click).
    // Once complete, Sprint/Sneak select the operating state, so stop intercepting.
    if (controls.CtrlKey && controls.ShiftKey && !be.StructureComplete)
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    // Before the converter exists: RMB tries to spawn it from carried materials.
    if (!be.IsConverterPresent())
    {
      if (be.TrySpawnConverter(byPlayer, out string spawnError))
      {
        (byPlayer as IClientPlayer)?.TriggerFpAnimation(
          EnumHandInteract.HeldItemInteract
        );
      }
      else if (world.Side == EnumAppSide.Client && spawnError.Length > 0)
      {
        (byPlayer as IClientPlayer)?.ShowChatNotification(spawnError);
      }
      return true;
    }

    ConverterOpState target = ResolveTarget(byPlayer);

    // Pouring is destructive (drains the entire charge), so rather than firing on
    // the click we begin a hold here and only commit once the pour-hold time has
    // elapsed in OnBlockInteractStep. Validate up front so the player isn't left
    // holding a doomed interaction; surface the same error a click would.
    if (target == ConverterOpState.Pouring)
    {
      if (be.OpState == ConverterOpState.Pouring)
        return false; // already pouring - nothing to hold for
      if (!be.CanOperate(out string pourError))
      {
        if (world.Side == EnumAppSide.Client && pourError.Length > 0)
          (byPlayer as IClientPlayer)?.ShowChatNotification(pourError);
        return false;
      }
      (byPlayer as IClientPlayer)?.TriggerFpAnimation(
        EnumHandInteract.HeldItemInteract
      );
      return true; // continue into OnBlockInteractStep
    }

    // Normal / Filling are non-destructive - apply immediately on click.
    if (be.TrySetState(byPlayer, target, out string error))
    {
      (byPlayer as IClientPlayer)?.TriggerFpAnimation(
        EnumHandInteract.HeldItemInteract
      );
    }
    else if (world.Side == EnumAppSide.Client && error.Length > 0)
    {
      (byPlayer as IClientPlayer)?.ShowChatNotification(error);
    }
    return true;
  }

  public override bool OnBlockInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityConverterControl be
    )
      return false;

    // Only the pour action is a held interaction. Stop the moment the player lets
    // go of sprint, the converter is gone, or it is already pouring.
    if (
      !be.IsConverterPresent()
      || ResolveTarget(byPlayer) != ConverterOpState.Pouring
      || be.OpState == ConverterOpState.Pouring
    )
      return false;

    if (secondsUsed < SmexValues.BessemerPourHoldSeconds)
      return true; // keep holding

    // Held long enough - commit the pour (TrySetState re-validates internally).
    be.TrySetState(byPlayer, ConverterOpState.Pouring, out _);
    return false;
  }

  // Operational intent from the held modifier keys: Sneak = filling,
  // Sprint = pouring, plain RMB = normal.
  private static ConverterOpState ResolveTarget(IPlayer byPlayer)
  {
    var controls = byPlayer.Entity.Controls;
    return controls.Sneak ? ConverterOpState.Filling
      : controls.Sprint ? ConverterOpState.Pouring
      : ConverterOpState.Normal;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
      is not BlockEntityConverterControl be
    )
      return baseHelp;

    // Construction phase: converter-spawn hint (the structure-projection hint is
    // contributed by the shared MultiblockStructure behavior via base).
    if (!be.IsConverterPresent())
    {
      return baseHelp
        .Append(
          new WorldInteraction
          {
            ActionLangCode = "smex:blockhelp-bessemer-spawn",
            MouseButton = EnumMouseButton.Right,
          }
        )
        .ToArray();
    }

    // Operational phase: state transition hints.
    return baseHelp
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "smex:blockhelp-bessemer-normal",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "smex:blockhelp-bessemer-filling",
          HotKeyCodes = ["sneak"],
          MouseButton = EnumMouseButton.Right,
        }
      )
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "smex:blockhelp-bessemer-pouring",
          HotKeyCodes = ["sprint"],
          MouseButton = EnumMouseButton.Right,
        }
      )
      .ToArray();
  }
}
