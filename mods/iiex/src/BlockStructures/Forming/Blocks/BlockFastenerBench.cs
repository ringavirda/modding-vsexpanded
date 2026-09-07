using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// The two fastener benches - the nail cutter and the riveter - as one blocktype with a <c>type</c> variant.
/// They are the same machine: an mpenergy consumer with a die fitted, a press that comes down on a stroke,
/// and a blank that goes in whole. Only the shape, the footprint and the fitted die differ, which is the
/// owner's own reading of the machining line and the reason there is one class here rather than two.
/// The simulation lives in <see cref="BlockEntityFastenerBench"/>.
/// See docs/design/mechanics/machining-line.md.
/// <para>
/// Both footprints are transcribed from workbench/machines.txt, each in the slice plane that
/// file draws it in - a <c>zy</c> elevation for the nail cutter, an <c>xy</c> one for the riveter. They are
/// not transposed onto a common plane: the two machines genuinely lie at right angles to each other, the
/// nail cutter running two cells deep with its shaft west-east and the riveter three cells wide with its
/// shaft north-south.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFastenerBench
  : BlockNetworkNode,
    IExBlockDefProvider,
    IFillerHost,
    IFillerInteractionTarget {
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The nail cutter's <c>type</c> variant.</summary>
  public const string NailCutter = "nailcutter";

  /// <summary>The riveter's <c>type</c> variant.</summary>
  public const string Riveter = "riveter";

  /// <summary>
  /// The bench blocktype: two machines, two horizontal orientations each. Authored in the <c>ns</c> frame
  /// (<c>rotateY:0</c>), which is the frame both drawings are exported in; <c>we</c> is the 90 degree
  /// rotation, exactly as the shear and the mill do it.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "forming", "forming/bench")
        .Class<BlockFastenerBench>()
        .EntityClass<BlockEntityFastenerBench>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        // No handbook groupBy. It is one wildcard for the whole blocktype, so either machine's value
        // names the other's codes and a riveter would gather nail cutters into its own slideshow.
        // Nothing is lost by omitting it: `groupBy` only ever gathers stacks the handbook already
        // lists, and CreativeCommon("*-ns") means each machine puts exactly one stack there.
        .VariantGroup("type", NailCutter, Riveter)
        .VariantGroup("orientation", "ns", "we")
        .NetworkOriented()
        .ShapeByType(
          $"*-{NailCutter}-ns",
          "iiex:forming/nailcutter",
          rotateY: 0
        )
        .ShapeByType(
          $"*-{NailCutter}-we",
          "iiex:forming/nailcutter",
          rotateY: 90
        )
        .ShapeByType($"*-{Riveter}-ns", "iiex:forming/riveter", rotateY: 0)
        .ShapeByType($"*-{Riveter}-we", "iiex:forming/riveter", rotateY: 90)
        .FillerOffsetsByType($"*-{NailCutter}-*", NailCutterFootprint)
        .FillerOffsetsByType($"*-{Riveter}-*", RiveterFootprint)
        .CreativeCommon("*-ns")
        .EntityBehavior("Animatable")
        // The press overhangs its cell; the placed cell is solid but must not cull neighbour faces.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprints

  /// <summary>
  /// The nail cutter, as machines.txt draws it - a <c>zy</c> elevation at fixed X, rows running down in
  /// -Y and columns along +Z:
  /// <code>
  /// I m
  /// O #
  /// </code>
  /// <c>O</c> is the principal, <c>I</c> the cell the player feeds and swaps dies at, and <c>m</c> the cell
  /// the drawn shaft runs through.
  /// </summary>
  private static readonly IReadOnlyList<FillerCellSpec> NailCutterFootprint =
    StructureFootprint.Layout(f =>
      f.Solid('I')
        .Solid('m')
        .Origin(0, 1)
        .Slice(
          0,
          """
          I m
          O #
          """
        )
    );

  /// <summary>
  /// The riveter, as machines.txt draws it - an <c>xy</c> elevation at fixed Z, rows running down in -Y and
  /// columns along +X:
  /// <code>
  /// # M #
  /// I O #
  /// </code>
  /// </summary>
  private static readonly IReadOnlyList<FillerCellSpec> RiveterFootprint =
    StructureFootprint.Layout(f =>
      f.Solid('I')
        .Solid('M')
        .Origin(-1, 1)
        .Face(
          0,
          """
          # M #
          I O #
          """
        )
    );

  /// <summary>Where the interaction cell sits for each machine, in the <c>ns</c> frame. The nail cutter
  /// puts it above the principal and the riveter beside it, which is the drawings' own difference.</summary>
  private static readonly Dictionary<string, Vec3i> Faces = new() {
    [NailCutter] = new Vec3i(0, 1, 0),
    [Riveter] = new Vec3i(-1, 0, 0),
  };

  /// <summary>The <c>fillerOffsets</c> attribute (from the code-first def), read by the shared helper.</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>Rotation in degrees applied to the <c>ns</c>-frame footprint to reach the placed orientation:
  /// <c>ns</c> 0 (authored), <c>we</c> 90, matching the per-orientation shape rotations.</summary>
  public int StructureAngle => Variant?["orientation"] == "we" ? 90 : 0;

  /// <summary>Which of the two benches this block is.</summary>
  public string MachineKey => Variant?["type"] ?? NailCutter;

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  /// <summary>The world position of the cell the player works at, rotated into the placed
  /// orientation.</summary>
  public BlockPos FaceCell(BlockPos pos) {
    Vec3i local = Faces.TryGetValue(MachineKey, out Vec3i? f)
      ? f
      : Faces[NailCutter];
    Vec3i r = ExOrientation.RotateOffset(local, StructureAngle);
    return pos.AddCopy(r.X, r.Y, r.Z);
  }

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    if (!StructureFillers.CanPlace(world, FootprintCells(blockSel.Position))) {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack? byItemStack = null
  ) {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    if (world.Side == EnumAppSide.Server)
      StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
  }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // Every removal path, not just a player break, so the reserved volume is never left behind as orphan
    // solid cells - the same reason the mill and the shear override this rather than OnBlockBroken.
    if (world.Side == EnumAppSide.Server)
      StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockRemoved(world, pos);
  }

  #endregion

  #region Interaction

  /// <summary>
  /// Routes a click on the footprint.
  /// <list type="bullet">
  ///   <item>Holding a wrench with a blank under the press: free it, unchanged.</item>
  ///   <item>Holding a die, anywhere on the machine: fit it, swapping out any die already there.</item>
  ///   <item>Holding a blank on the working face: press it.</item>
  ///   <item>Sneaking empty-handed on the face: take the fitted die back.</item>
  /// </list>
  /// The same four verbs as the shear, because it is the same machine.
  /// </summary>
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel, clickedCell);

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
      is not BlockEntityFastenerBench bench
    )
      return false;

    ItemSlot? slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
    ItemStack? held = slot?.Itemstack;

    if (IsWrench(held) && bench.IsStroking)
      return FreeStuckBlank(world, byPlayer, bench);
    if (ItemDie.IsDie(held))
      return FitDie(world, byPlayer, bench, slot!);
    if (held == null)
      return byPlayer.Entity.Controls.ShiftKey
        && TakeDie(world, byPlayer, bench);

    return clickedCell.Equals(FaceCell(sel.Position))
      && Press(world, byPlayer, bench, held);
  }

  private static bool IsWrench(ItemStack? stack) =>
    stack?.Collectible?.Code?.FirstCodePart() == "wrench";

  private static bool FreeStuckBlank(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityFastenerBench bench
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack? freed = bench.ReleaseStuckBlank();
    if (freed != null && !byPlayer.InventoryManager.TryGiveItemstack(freed))
      world.SpawnItemEntity(freed, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool FitDie(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityFastenerBench bench,
    ItemSlot slot
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack offered = slot.TakeOut(1);
    if (!bench.TryFitDie(offered, out ItemStack? previous)) {
      // Refused mid-stroke: the die the player offered goes straight back, or a swap attempted with a
      // blank under the press would eat it.
      slot.Itemstack = offered;
      slot.MarkDirty();
      return true;
    }

    slot.MarkDirty();
    if (
      previous != null
      && !byPlayer.InventoryManager.TryGiveItemstack(previous)
    )
      world.SpawnItemEntity(previous, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool TakeDie(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityFastenerBench bench
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;
    if (!bench.TryFitDie(null, out ItemStack? previous) || previous == null)
      return true;

    if (!byPlayer.InventoryManager.TryGiveItemstack(previous))
      world.SpawnItemEntity(previous, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool Press(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityFastenerBench bench,
    ItemStack held
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    BenchDecision decision = bench.TryPress(held);
    if (!decision.Accepted) {
      Report(byPlayer, decision.Verdict);
      return true;
    }

    // The offered stack is consumed one at a time: the blank under the press is the machine's now.
    byPlayer.InventoryManager?.ActiveHotbarSlot?.TakeOut(1);
    byPlayer.InventoryManager?.ActiveHotbarSlot?.MarkDirty();
    return true;
  }

  /// <summary>A press is a single click, so neither hold step matters here.</summary>
  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  /// <inheritdoc cref="OnFillerInteractStep"/>
  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  /// <summary>The help shown against the working face. Only that cell takes a blank, so clicking anywhere
  /// else on the footprint offers nothing to explain.</summary>
  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is not BlockEntityFastenerBench
      || !clickedCell.Equals(FaceCell(principalSel.Position))
    )
      return [];

    return
    [
      new WorldInteraction
      {
        ActionLangCode = IiexLang.BenchHelpPress,
        MouseButton = EnumMouseButton.Right,
      },
      new WorldInteraction
      {
        ActionLangCode = IiexLang.BenchHelpTakedie,
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "sneak",
      },
      new WorldInteraction
      {
        ActionLangCode = IiexLang.BenchHelpFree,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = [],
      },
    ];
  }

  /// <summary>Tells the player which refusal they hit, since every one has a different fix. The order
  /// <see cref="BenchFeed.Decide"/> reports them in is the order they can be fixed in.</summary>
  private static void Report(IPlayer byPlayer, BenchVerdict verdict) =>
    (byPlayer as IServerPlayer)?.SendIngameError(
      verdict switch {
        BenchVerdict.NoDie => "iiex-bench-nodie",
        BenchVerdict.NoJob => "iiex-bench-nojob",
        BenchVerdict.NotTurning => "iiex-bench-notturning",
        _ => "iiex-bench-notenoughdrive",
      }
    );

  #endregion
}
