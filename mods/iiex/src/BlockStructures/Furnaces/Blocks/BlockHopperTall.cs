using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The tall hopper: a two-cell-high burden tank that drips its contents into the furnace shaft below it
/// (<see cref="BlockEntityHopperTall"/>). It occupies one grid cell, the base, and renders a two-block-high
/// model up through its top filler cell. Every player interaction lands on that filler cell, which reroutes
/// right-clicks to the base's block entity through <see cref="IFillerInteractionTarget"/>, so burden is
/// poured in and drawn out at the top. Holds either burden family; the furnace it feeds gates acceptance.
/// </summary>
[BlockRegister]
public partial class BlockHopperTall
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopper-tall", "hopper/tall")
        .Class<BlockHopperTall>()
        .EntityClass<BlockEntityHopperTall>()
        // Build-outline projection: the hopper is a functional cell of the furnace layout, so a player at
        // the hopper can preview and complete an incomplete furnace. The base cell fires this behaviour;
        // the top filler cell routes the gesture through HandleInteract. Must precede other rmb consumers.
        .Behavior("MultiblockStructure")
        // Stamps the side variant at placement, as on every furnace core.
        .Behavior("ExOrientable")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .Shape("iiex:hopper-tall")
        .CreativeCommon("*")
        // The footprint is a single filler one cell up (the hopper is 2 tall over its base): '#' over the
        // '0' principal with Origin(0, 1) resolves to the cell at (0, +1, 0), the top cell the model fills
        // and every interaction routes through.
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 1)
              .Slice(
                0,
                """
                #
                0
                """
              )
          )
        )
        .SolidNonOpaque()
        // Declared last so the code reads hopper-tall-{side}. The side variant lets a multiblock layout
        // demand a correctly-facing hopper: the oriented-parts rule rotates the required facing with the
        // structure, so a rotated cupola does not charge outside itself. The drip reads this variant
        // directly, without an anchor lookup. The four states are inherited from vanilla's property rather
        // than re-typed.
        .SideVariant(),
    ];

  #endregion

  /// <summary>The hopper's own placement angle. The footprint is rotation-invariant because the filler sits
  /// directly above the base, but the drip is not: it searches neighbours, which must rotate.</summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]);

  #region Drops

  // Placement, the top filler cell and its removal on break are handled by BlockFilledMegastructure. A
  // broken hopper drops itself plus whatever burden the tank still held.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    var drops = new List<ItemStack>(
      base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)
    );
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperTall be
      && be.TankContents is { } burden
    )
      drops.Add(burden.Clone());
    return [.. drops];
  }

  #endregion

  #region Interaction (routed from the top filler cell)

  // Deposit and withdraw are reachable only through the top filler cell, which reroutes those clicks here.
  // A right-click with burden fills the tank (ctrl transfers the whole held stack); an empty-handed
  // right-click empties it.
  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
      is not BlockEntityHopperTall be
    )
      return false;

    // Consume the build-outline gesture before the deposit/withdraw path so Ctrl+Shift+right-click
    // previews the incomplete furnace instead of emptying the tank. The base cell gets this from the
    // shared MultiblockStructure behaviour; this covers the filler-routed clicks the behaviour never sees.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        principalSel.Position
      )
    )
      return true;

    if (world.Side == EnumAppSide.Server) {
      ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
      if (active?.Empty == false) {
        // A mismatched grade or family is reported, since one stack cannot hold two. A full tank is
        // already shown by the block info, so it passes silently.
        if (
          !be.TryDeposit(active, byPlayer.Entity.Controls.CtrlKey) && !be.IsFull
        )
          (byPlayer as IServerPlayer)?.SendIngameError(
            "iiex-hoppertall-wronggrade"
          );
      } else {
        ItemStack? taken = be.TryWithdraw();
        if (
          taken != null
          && byPlayer.InventoryManager?.TryGiveItemstack(taken) != true
        )
          world.SpawnItemEntity(
            taken,
            principalSel.Position.ToVec3d().Add(0.5, 0.5, 0.5)
          );
      }
    }
    // Swallow the click on both sides so no block is placed against the filler/hopper face.
    return true;
  }

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => HopperInteractionHelp(world, principalSel);

  #endregion

  #region Interaction help

  // Resolved once (the block is a singleton): a burden stack shown in the "add" hints.
  private ItemStack[]? _burdenStack;

  private WorldInteraction[] HopperInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel
  ) {
    ItemStack[] burden = _burdenStack ??= ResolveBurdenStack();
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iiex:hoppertall-help-add",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = burden,
      },
      new()
      {
        ActionLangCode = "iiex:hoppertall-help-addstack",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = burden,
      },
    };
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is BlockEntityHopperTall be
      && be.TankCount > 0
    )
      help.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:hoppertall-help-take",
          MouseButton = EnumMouseButton.Right,
        }
      );

    // While the furnace this hopper charges is incomplete, offer the build-outline gesture here as well.
    // The base cell shows it via the shared behaviour; the top filler cell routes its help through this.
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is BlockEntityHopperTall hopper
      && hopper.ResolveOwningAnchor() is { StructureComplete: false }
    )
      help.AddRange(BlockBehaviorMultiblockStructure.ProjectionHelp(this));

    return [.. help];
  }

  private ItemStack[] ResolveBurdenStack() {
    Item? burden = api.World.GetItem(new AssetLocation("iiex", "burden"));
    return burden == null ? [] : [new ItemStack(burden)];
  }

  #endregion
}
