using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Storage;
using IronIndustryExpanded.BlockStructures.Storage.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Storage.Blocks;

/// <summary>
/// A wooden rack: a row of cells that holds stock by its length. Three one-cell stacks, a two-cell stack
/// beside a one-cell one, or one three-cell stack all fill the shipped rack, and every cell works the
/// whole rack. What an item occupies is declared per store in <c>config/bayoccupancy/</c>, and how many
/// cells a rack has is its own footprint - so a longer rack is another blocktype, not a code change.
/// See docs/design/machines/stock-rack.md.
/// </summary>
[BlockRegister]
public partial class BlockStorageRack
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  /// <summary>The catalogue key this rack's occupancy rules are declared under.</summary>
  public const string StoreKey = "storagerack";

  #region Code-first definition

  /// <summary>
  /// The two filler cells north of the principal, drawn as the art is: three bays running -Z from the
  /// anchor. <c>'+'</c> rather than <c>'#'</c> - the cells allow attachment, because racks stack
  /// vertically and shelving is just racks, so the next rack up must be able to land on any cell of the
  /// one below rather than only on its principal.
  /// </summary>
  private static readonly IReadOnlyList<FillerCellSpec> Footprint =
    StructureFootprint.Layout(f =>
      // One XZ floor plan at y=0; rows run +Z from the top-left, so the principal is the last row.
      f.Origin(0, -2)
        .Layer(
          0,
          """
          +
          +
          O
          """
        )
    );

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "storage-rack", "storage/storagerack")
        .Class<BlockStorageRack>()
        .EntityClass<BlockEntityStorageRack>()
        .Behavior("ExOrientable")
        .SideVariant()
        .CreativeCommon("*-n")
        .ShapeSpunPerOrientation("iiex:storage/storagerack")
        .Material(EnumBlockMaterial.Wood)
        .Resistance(3f)
        .MaterialDensity(600)
        .FillerOffsets(Footprint)
        .Sound("walk", "walk/wood")
        .Sound("place", "block/planks")
        .SoundByTool(EnumTool.Axe, "block/chop-hit-axe", "block/chop-break-axe")
        .SolidNonOpaque(),
    ];

  #endregion

  /// <inheritdoc/>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant?["side"]);

  #region Cells

  /// <summary>
  /// This rack's cells in its own frame, principal first and then outward along whichever horizontal axis
  /// the footprint runs. Read off the footprint rather than declared, so a rack of another length is a
  /// definition change and nothing here moves. Empty when the block carries no footprint at all, which is
  /// how a stand-in in a test reads.
  /// </summary>
  public IReadOnlyList<Vec3i> LocalCells =>
    _localCells ??= OrderedCells(StructureFillers.ReadOffsets(FillerOffsets));

  private IReadOnlyList<Vec3i>? _localCells;

  /// <summary>How many stacks-worth of length this rack holds.</summary>
  public int Cells => LocalCells.Count;

  /// <summary>
  /// The principal plus <paramref name="fillers"/>, sorted along the one horizontal axis they vary on.
  /// A footprint that varies on both is not a row and yields the principal alone, so a rack drawn wrong
  /// holds one thing rather than mapping clicks onto cells that are not in line.
  /// </summary>
  private static IReadOnlyList<Vec3i> OrderedCells(
    IReadOnlyList<FillerOffset> fillers
  ) {
    var cells = new List<Vec3i> { new(0, 0, 0) };
    foreach (FillerOffset cell in fillers)
      cells.Add(cell.Offset);

    bool variesX = cells.Any(c => c.X != 0);
    bool variesZ = cells.Any(c => c.Z != 0);
    if (cells.Any(c => c.Y != 0) || (variesX && variesZ))
      return [new Vec3i(0, 0, 0)];

    // Sorted by distance from the principal, so cell 0 is always the anchor whichever way the row runs.
    return
    [
      .. cells.OrderBy(c =>
        variesX ? System.Math.Abs(c.X) : System.Math.Abs(c.Z)
      ),
    ];
  }

  /// <summary>
  /// Which cell of this rack <paramref name="clicked"/> is, or null when it is not one. The footprint
  /// turns with the block, so the world offset is rotated back into the rack's own frame first; otherwise
  /// a rack facing west has its near and far ends swapped.
  /// </summary>
  public int? CellAt(BlockPos principal, BlockPos clicked) {
    Vec3i world = new(
      clicked.X - principal.X,
      clicked.Y - principal.Y,
      clicked.Z - principal.Z
    );
    Vec3i local = ExOrientation.RotateOffset(world, -StructureAngle);

    for (int i = 0; i < LocalCells.Count; i++)
      if (LocalCells[i].Equals(local))
        return i;
    return null;
  }

  #endregion

  #region Interaction

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    Worked(world, byPlayer, blockSel.Position, blockSel.Position)
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  /// <inheritdoc/>
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => Worked(world, byPlayer, principalSel.Position, clickedCell);

  /// <inheritdoc/>
  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  /// <inheritdoc/>
  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  /// <summary>
  /// One click on one cell: a held stack the catalogue sizes goes on, an empty hand takes back whatever
  /// covers that cell. Laying is not gated on the clicked cell - the rack finds the lowest run that fits
  /// - because a player holding a three-cell slab should not have to work out which end to click.
  /// </summary>
  private bool Worked(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principal,
    BlockPos clicked
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principal)
      is not BlockEntityStorageRack rack
    )
      return false;
    if (CellAt(principal, clicked) is not { } cell)
      return false;

    ItemSlot? held = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (held?.Itemstack != null)
      return rack.TryLay(held, world.Side == EnumAppSide.Server);

    ItemStack? taken = rack.TryTake(cell, world.Side == EnumAppSide.Server);
    if (taken == null)
      return false;
    // Spawned at the clicked cell when the player has no room, so a full inventory never eats the piece.
    if (
      world.Side == EnumAppSide.Server
      && byPlayer.InventoryManager?.TryGiveItemstack(taken) != true
    )
      world.SpawnItemEntity(taken, clicked.ToVec3d().Add(0.5, 0.6, 0.5));
    return true;
  }

  #endregion

  #region Drops

  /// <summary>
  /// Spawns what the rack was holding before the block goes. Breaking any cell breaks the whole
  /// mega-block - a filler reroutes being broken to the principal - so without this a rack full of slabs
  /// is eaten by one misclick, which is the worst bug this block can have and one missing call.
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityStorageRack rack
    )
      foreach (ItemStack stack in rack.TakeAll())
        world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.6, 0.5));

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion

  #region Interaction help

  /// <inheritdoc/>
  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => GetPlacedBlockInteractionHelp(world, selection, forPlayer);

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    [
      .. base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [],
      new WorldInteraction
      {
        ActionLangCode = "iiex:blockhelp-rack-lay",
        MouseButton = EnumMouseButton.Right,
        ShouldApply = (wi, bs, es) => !forPlayer.Entity.RightHandItemSlot.Empty,
      },
      new WorldInteraction
      {
        ActionLangCode = "iiex:blockhelp-rack-take",
        MouseButton = EnumMouseButton.Right,
        ShouldApply = (wi, bs, es) => forPlayer.Entity.RightHandItemSlot.Empty,
      },
    ];

  #endregion
}
