using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.OreProcessing.Blocks;

/// <summary>
/// A 9-cell mega-block stock house. Two hoppers (ore and flux) sit over a shared bunker basin with one
/// sliding gate between them; opening the gate drops both hoppers together into the basin, where the
/// burden collects until the player takes it out. Proportioning is a single ore-to-flux ratio, so there
/// is no batch, no lock and no cycle. The machine has no mechanism, and therefore no MP port and no
/// <c>lpex:</c> construction ingredient, which keeps it buildable in an iwex-only install.
/// See docs/design/machines/burdenmaker.md.
/// </summary>
[BlockRegister]
public partial class BlockBurdenmaker
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The burdenmaker blocktype: a 9-cell footprint drawn as two floor plans, and a five-stage
  /// right-click construction whose stages are the shape's own element groups, masonry first and
  /// ironwork last. The <c>brick</c> variant group parameterises the shape's <c>fire1</c> texture slot,
  /// so the code is <c>iwex:burdenmaker-{brick}-{side}</c>.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "burdenmaker", "ore/burdenmaker")
        .Class<BlockBurdenmaker>()
        .EntityClass<BlockEntityBurdenmaker>()
        .Material(EnumBlockMaterial.Ceramic)
        .MiningTier(0)
        .Resistance(3.5f)
        .MaxStackSize(1)
        .NoDrops()
        // Rows run +Z from z = -1 and columns +X from x = -1, so the front half (z = -1) carries the
        // hoppers and the back half (z = 0) is left open at y = 1 for the player to reach into the basin.
        // 'O' marks the principal and is skipped, so the emitted table is 8 cells, not 9.
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(-1, -1)
              .Layer(
                0,
                """
                # # #
                # O #
                """
              )
              .Layer(
                1,
                """
                # # #
                . . .
                """
              )
          )
        )
        .Behavior("ExOrientable")
        .Behavior("BlockEntityInteract")
        .EntityBehavior("Animatable")
        .Construction(c =>
          c.Stage(s => s.AddElements("Root/Base"))
            .Stage(s =>
              s.Require(
                  "game:burnedbrick-{brick}",
                  12,
                  "iwex:rcc-ingredient-brick"
                )
                .AddElements("Root/BaseExtension")
            )
            .Stage(s =>
              s.Require(
                  "game:burnedbrick-{brick}",
                  16,
                  "iwex:rcc-ingredient-brick"
                )
                .AddElements("Root/HopperMasonry")
            )
            .Stage(s =>
              s.Require(
                  "game:metalplate-iron",
                  6,
                  "iwex:rcc-ingredient-hopperplate"
                )
                .AddElements("Root/Hoppers")
            )
            .Stage(s =>
              s.Require(
                  "game:metalplate-iron",
                  3,
                  "iwex:rcc-ingredient-lidplate"
                )
                .Require("game:ingot-iron", 2, "iwex:rcc-ingredient-lidrails")
                .AddElements("Root/Lids")
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
        .SideVariant()
        .CreativeTab("general", "*-cream-n")
        .CreativeTab("iwex", "*-cream-n")
        .ShapeSpunPerOrientation("iwex:ore/burdenmaker")
        // The placed shell before any stage is built: the basin floor only, marking where the machine
        // will stand.
        .ShapeSelectiveElements("Root/Base/*")
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
        .Sound("walk", "game:walk/stone"),
    ];

  #endregion

  /// <summary>
  /// Structure/filler rotation, with no <c>+180</c> correction: the drawn model spans z = -16..+16 px the
  /// same way the footprint does (<c>BaseExtension</c> at z -16..4 is cell z = -1, <c>Base</c> at z 0..16
  /// is cell z = 0), so model and footprint agree. Adding 180 here would put the hoppers behind the basin.
  /// </summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]);

  #region Drops

  // Placement, the filler footprint and break-time filler removal are handled by BlockFilledMegastructure.

  // A broken burdenmaker returns its construction materials (scattered by the RightClickConstructable
  // behaviour) plus both hopper contents and the basin (spilled by the container BE). It never returns
  // the block itself: it is raised, not placed.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Cell classification

  // Public rather than internal: iwex declares no `InternalsVisibleTo`, so an internal classifier could
  // not be covered by tests.

  /// <summary>
  /// What a footprint cell of the burdenmaker is. The machine has no GUI, so the clicked cell selects
  /// the action.
  /// </summary>
  public enum BurdenmakerCell {
    /// <summary>The wide upper hopper - crushed or roasted iron ore. Two cells.</summary>
    OreHopper,

    /// <summary>The narrow upper hopper - lime. One cell.</summary>
    FluxHopper,

    /// <summary>The principal: the sliding lid under both hoppers.</summary>
    Gate,

    /// <summary>The shared basin the burden collects in. Five cells.</summary>
    Bunker,

    /// <summary>Not part of this machine.</summary>
    Outside,
  }

  /// <summary>
  /// Classifies the world cell <paramref name="clicked"/> against a burdenmaker whose principal is at
  /// <paramref name="principal"/> and which was placed at <paramref name="structureAngle"/>. The inverse
  /// rotation is required: the two hoppers differ by X, so comparing raw world coordinates would put ore
  /// in the flux hopper on the facings where X and Z have swapped, while reading correctly on north.
  /// Static and world-free, so the per-facing table is testable without a world.
  /// </summary>
  public static BurdenmakerCell Classify(
    BlockPos principal,
    BlockPos clicked,
    int structureAngle
  ) {
    // World delta -> the authored north frame. RotateOffset normalises, so -90 arrives as 270.
    Vec3i local = ExOrientation.RotateOffset(
      clicked.X - principal.X,
      clicked.Y - principal.Y,
      clicked.Z - principal.Z,
      -structureAngle
    );

    // y = 1 carries the hoppers across the front row only (z = -1); y = 0 is the basin, with the
    // principal itself the gate. Anything else belongs to another block.
    return (local.X, local.Y, local.Z) switch {
      ( >= -1 and <= 0, 1, -1) => BurdenmakerCell.OreHopper,
      (1, 1, -1) => BurdenmakerCell.FluxHopper,
      (0, 0, 0) => BurdenmakerCell.Gate,
      ( >= -1 and <= 1, 0, -1 or 0) => BurdenmakerCell.Bunker,
      _ => BurdenmakerCell.Outside,
    };
  }

  #endregion

  #region Interaction

  /// <summary>
  /// A click on the principal, which is the gate cell, so this is the lid toggle. Every other cell
  /// arrives through the fillers.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  /// <summary>
  /// Routes a click to the action its cell carries. Returns <c>null</c> before construction completes, so
  /// the click falls through to the RCC behaviour and the machine can still be built.
  /// </summary>
  private bool? HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityBurdenmaker be
      || !be.IsConstructed
    )
      return null; // pre-construction clicks drive the RCC behaviour

    BurdenmakerCell cell = Classify(be.Pos, clickedCell, StructureAngle);
    if (cell == BurdenmakerCell.Outside)
      return null;

    if (world.Side == EnumAppSide.Client)
      return true; // the server owns every mutation below; the click is still consumed

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    // Ctrl, not sneak: vanilla ground-storage placement already takes sneak + right-click with a held
    // item, so a sneak idiom here would collide with it.
    bool wholeStack = byPlayer.Entity.Controls.CtrlKey;

    switch (cell) {
      case BurdenmakerCell.Gate:
        if (!be.ToggleGate(out string? error) && error != null)
          (byPlayer as IServerPlayer)?.SendIngameError(error);
        break;

      case BurdenmakerCell.OreHopper:
        if (active?.Empty == false)
          be.TryLoadOre(active, wholeStack);
        else
          GiveBack(world, byPlayer, sel, be.TryTakeOre());
        break;

      case BurdenmakerCell.FluxHopper:
        if (active?.Empty == false)
          be.TryLoadFlux(active, wholeStack);
        else
          GiveBack(world, byPlayer, sel, be.TryTakeFlux());
        break;

      case BurdenmakerCell.Bunker:
        // Take-only, whatever is held: the basin is filled by the gate and by nothing else.
        GiveBack(world, byPlayer, sel, be.TryWithdrawBurden());
        break;
    }

    // Swallowed on both sides so no block is placed against the machine's face.
    return true;
  }

  private static void GiveBack(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    ItemStack? taken
  ) {
    if (taken == null)
      return;
    if (byPlayer.InventoryManager?.TryGiveItemstack(taken) != true)
      world.SpawnItemEntity(taken, sel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
  }

  #endregion

  #region Interaction help

  // Resolved once and cached: the block is a singleton, and resolving walks every collectible in the
  // game, which is too costly per frame of the help overlay.
  private ItemStack[]? _oreStacks;
  private ItemStack[]? _fluxStacks;

  /// <summary>
  /// Help for the principal, which is the gate cell. Every other cell arrives through
  /// <see cref="IFillerInteractionTarget.GetFillerInteractionHelp"/>.
  /// </summary>
  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => BuildInteractionHelp(world, selection, forPlayer, BurdenmakerCell.Gate);

  /// <summary>
  /// The hints for whichever cell is being looked at. The machine has no GUI, so the help text is the
  /// only thing distinguishing the two hoppers; it is routed by cell off the same <see cref="Classify"/>
  /// call the interaction uses. Before construction completes it defers to the base help, where the RCC
  /// behaviour advertises the next stage's materials.
  /// </summary>
  private WorldInteraction[] BuildInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer,
    BurdenmakerCell cell
  ) {
    WorldInteraction[] baseHelp = base.GetPlacedBlockInteractionHelp(
      world,
      selection,
      forPlayer
    );

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is not BlockEntityBurdenmaker be
      || !be.IsConstructed
    )
      return baseHelp; // the RCC behaviour supplies the construction help

    var help = new List<WorldInteraction>();
    switch (cell) {
      case BurdenmakerCell.OreHopper:
        AddLoadHints(
          help,
          "iwex:burdenmaker-help-addore",
          "iwex:burdenmaker-help-addore-stack",
          _oreStacks ??= ResolveStacks(BlockEntityBurdenmaker.IsOre)
        );
        break;

      case BurdenmakerCell.FluxHopper:
        AddLoadHints(
          help,
          "iwex:burdenmaker-help-addflux",
          "iwex:burdenmaker-help-addflux-stack",
          _fluxStacks ??= ResolveStacks(BlockEntityBurdenmaker.IsFlux)
        );
        break;

      case BurdenmakerCell.Gate:
        help.Add(
          new WorldInteraction {
            ActionLangCode = "iwex:burdenmaker-help-gate",
            MouseButton = EnumMouseButton.Right,
          }
        );
        break;
    }

    // Every cell except the gate hands something back to an empty hand: a hopper returns its own
    // material, the basin returns burden.
    if (cell != BurdenmakerCell.Gate && cell != BurdenmakerCell.Outside)
      help.Add(
        new WorldInteraction {
          ActionLangCode = "iwex:burdenmaker-help-take",
          MouseButton = EnumMouseButton.Right,
        }
      );

    return [.. help, .. baseHelp];
  }

  private static void AddLoadHints(
    List<WorldInteraction> help,
    string one,
    string stack,
    ItemStack[] accepted
  ) {
    help.Add(
      new WorldInteraction {
        ActionLangCode = one,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = accepted,
      }
    );
    // Ctrl, matching the interaction itself; vanilla ground-storage placement holds sneak.
    help.Add(
      new WorldInteraction {
        ActionLangCode = stack,
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = accepted,
      }
    );
  }

  /// <summary>
  /// A representative stack of every collectible one hopper accepts, so the help overlay cycles through
  /// them. Acceptance is the registry's test rather than a fixed list, so ores contributed by other mods
  /// are included.
  /// </summary>
  private ItemStack[] ResolveStacks(System.Func<ItemStack?, bool> accepts) {
    if (api?.World?.Collectibles is not { } collectibles)
      return [];

    var stacks = new List<ItemStack>();
    foreach (CollectibleObject collectible in collectibles) {
      if (collectible?.Code == null)
        continue;
      var stack = new ItemStack(collectible);
      if (accepts(stack))
        stacks.Add(stack);
    }
    return [.. stacks];
  }

  #endregion

  #region Filler interaction forwarding

  // A click on any reserved footprint cell drives the principal. The clicked cell is carried through
  // rather than discarded, because the cell selects the action: the two upper front cells are the ore
  // hopper, the third is the flux hopper, the principal is the gate and the rest of the basin hands
  // burden back.
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteract(world, byPlayer, principalSel, clickedCell)
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

  // The help is classified from the clicked cell exactly as the interaction is. Forwarding to
  // GetPlacedBlockInteractionHelp instead would show the gate's hint on all eight filler cells.
  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) =>
    BuildInteractionHelp(
      world,
      principalSel,
      forPlayer,
      Classify(principalSel.Position, clickedCell, StructureAngle)
    );

  #endregion
}
