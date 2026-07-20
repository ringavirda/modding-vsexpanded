using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.OreProcessing.Blocks;

/// <summary>
/// The ore/blast-mix storage bunker mega-block. Occupies one grid cell (the principal) but renders
/// across a 3×1×6 footprint reserved with invisible structure fillers (real per-cell collision);
/// construction is driven by the RightClickConstructable behavior authored on this class. The brick
/// <c>brick</c> variant only swaps the wall texture, so all colours share this one class.
/// </summary>
[BlockRegister]
public partial class BlockOreBunker
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The bunker blocktype, authored in C# (migrated from ore/bunker.json). The footprint is a
  /// <b>computed</b> 3-wide × 6-deep floor (<see cref="StructureFootprint.Rectangle"/>) and the
  /// construction sequence is a typed stage table - the data-table win of code-first for a machine.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Bunker(domain)];

  private static ExBlockDef Bunker(string domain) =>
    ExBlockDef
      .Create(domain, "bunker", "ore/bunker")
      .Class<BlockOreBunker>()
      .EntityClass<BlockEntityOreBunker>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(1)
      .NoDrops()
      .FillerOffsets(StructureFootprint.Rectangle(halfWidth: 1, depth: 6))
      .Behavior("HorizontalOrientable")
      .Behavior("BlockEntityInteract")
      .EntityBehavior("Animatable")
      .Construction(c =>
        c.Stage(s => s.AddElements("Root/InputBase"))
          .Stage(s =>
            s.Require(
                "game:burnedbrick-{brick}",
                8,
                "iwex:rcc-ingredient-brick"
              )
              .AddElements("Root/Base")
          )
          .Stage(s =>
            s.Require(
                "game:burnedbrick-{brick}",
                24,
                "iwex:rcc-ingredient-brick"
              )
              .AddElements("Root/Walls")
          )
      )
      .VariantGroup(
        "brick",
        "black",
        "brown",
        "cream",
        "gray",
        "orange",
        "red",
        "tan"
      )
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .CreativeTab("general", "*-north")
      .CreativeTab("iwex", "*-north")
      .Shape("iwex:ore/bunker")
      .ShapeRotateYByType("*-north", 180)
      .ShapeRotateYByType("*-east", 90)
      .ShapeRotateYByType("*-south", 0)
      .ShapeRotateYByType("*-west", 270)
      .ShapeSelectiveElements("Root/InputBase/*")
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  #endregion

  /// <summary>
  /// Structure/filler rotation, paired with the shape <c>rotateYByType</c>. The +180 keeps the footprint
  /// flush with the model, whose shape is authored facing the opposite way from the orientation
  /// convention (so a placed bunker extends away from the player, not into them). Also rotates the BE's
  /// render/collision boxes (multiblock-structure verification).
  /// </summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]) + 180;

  #region Drops

  // Placement, the filler footprint and break-time filler removal are handled by
  // BlockFilledMegastructure; the container BE spills its stored contents in the base break call.

  // A broken bunker returns only its construction materials (scattered by the RightClickConstructable
  // behaviour) plus its stored contents (spilled by the container BE), never the bunker block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Crate interaction

  // A finished bunker behaves like a large, GUI-less crate: right-click with burden deposits the
  // held stack, an empty-handed right-click withdraws a stack. Before construction completes the
  // click falls through to the RightClickConstructable behavior instead (HandleInteract returns null).
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  private bool? HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityOreBunker be
      || !be.IsConstructed
    )
      return null; // pre-construction clicks drive the RCC behavior

    if (world.Side == EnumAppSide.Server)
    {
      ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
      if (active?.Empty == false)
      {
        // Plain right-click deposits one burden; ctrl+right-click deposits the whole held stack. (Ctrl,
        // not sneak: sneak+right-click with a held item is taken by vanilla ground-storage placement.)
        be.TryDeposit(active, byPlayer.Entity.Controls.CtrlKey);
      }
      else
      {
        ItemStack? taken = be.TryWithdraw();
        if (
          taken != null
          && byPlayer.InventoryManager?.TryGiveItemstack(taken) != true
        )
          world.SpawnItemEntity(
            taken,
            sel.Position.ToVec3d().Add(0.5, 0.5, 0.5)
          );
      }
    }
    // A finished bunker swallows the click on both sides so no block is placed against its face.
    return true;
  }

  #endregion

  #region Filler interaction forwarding

  // A click on any reserved footprint cell drives the principal's behaviours (the crate add/take
  // when finished, the RCC construction before that).
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteract(world, byPlayer, principalSel)
    ?? base.OnBlockInteractStart(world, byPlayer, principalSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => GetPlacedBlockInteractionHelp(world, principalSel, forPlayer);

  #endregion

  #region Interaction help

  // Resolved once (block is a singleton): a burden stack shown in the "add" hint.
  private ItemStack[]? _burdenStack;

  // A finished bunker reads like a vanilla crate: right-click with burden to add, empty-handed to take.
  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    WorldInteraction[] baseHelp = base.GetPlacedBlockInteractionHelp(
      world,
      selection,
      forPlayer
    );

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is not BlockEntityOreBunker be
      || !be.IsConstructed
    )
      return baseHelp; // RCC behaviour supplies the construction help

    ItemStack[] burden = _burdenStack ??= ResolveBurdenStack();
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:bunker-help-add",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = burden,
      },
      new()
      {
        ActionLangCode = "iwex:bunker-help-add-stack",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = burden,
      },
    };
    if (be.TotalContents > 0)
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:bunker-help-take",
          MouseButton = EnumMouseButton.Right,
        }
      );

    return [.. help, .. baseHelp];
  }

  private ItemStack[] ResolveBurdenStack()
  {
    Item? burden = api.World.GetItem(new AssetLocation("iwex", "burden"));
    return burden == null ? [] : [new ItemStack(burden)];
  }

  #endregion
}
