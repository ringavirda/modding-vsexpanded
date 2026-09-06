using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity for a shaft-furnace tap-hole, iron notch and cinder notch alike. Holds the plug state and
/// accepts molten metal pushed from the furnace to hand down into the canal start beneath it.
/// </summary>
[BlockEntityRegister]
public class BlockEntityFurnaceTap : ExBlockEntity, IMultiblockComponent {
  /// <summary>
  /// Whether the tap-hole is stopped with a clay plug. True on a newly built tap: a tap arrives closed,
  /// which is what makes blowing a furnace in cost a plug. Drawn by <see cref="OnTesselation"/> rather
  /// than posed by an animator.
  /// </summary>
  public bool IsPlugged { get; private set; } = true;

  /// <summary>Whether the tap is open and pouring - the plug's other face. An opened tap does not close
  /// itself: it runs until the crucible empties or the player re-plugs it.</summary>
  public bool IsPouring => !IsPlugged;

  // Both taps of a furnace run this block entity; each scans up to its furnace core and shows the pool it
  // drains - the lower (metal) tap the metal, the upper the slag. Which one a tap is is decided by comparing
  // its cell against the core's Metal/SlagTapPos, not by the block type, because the position also answers
  // whether the furnace's drawing marks that cell as a tap at all: a reverberatory hearth marks neither, and
  // a tap stuck on one must show no pool. The same link answers the build-outline projection
  // (ResolveOwningAnchor).
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  /// <summary>The furnace this tap belongs to, resolved by scanning up to the core whose layout owns the
  /// tap's cell (cached and throttled by the link). Drives the pool HUD and the shared build outline.</summary>
  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <inheritdoc/>
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() =>
    Anchor.Resolve();

  #region The plug

  /// <summary>Sets the plug state and republishes it. Server-side work: the flag is save data and the
  /// client learns it through the tree.</summary>
  public void SetPlugged(bool plugged) {
    if (IsPlugged == plugged)
      return;
    IsPlugged = plugged;
    MarkDirty(true);
  }

  #endregion

  #region Render

  // The plug is a shape state, not a pose: a plugged tap draws `ClayPlug` and an open one does not. Both
  // tap shapes carry exactly three top-level elements - Base, TapCanal, ClayPlug - so one pair of
  // keep-lists drives both types through one code path.
  private static readonly string[] PluggedElements =
  [
    "Base",
    "TapCanal",
    "ClayPlug",
  ];
  private static readonly string[] OpenElements = ["Base", "TapCanal"];

  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Api is not ICoreClientAPI capi)
      return base.OnTesselation(mesher, tesselator);

    if (
      ExMeshCache.GetOrCreate(
        capi,
        Block,
        IsPlugged ? "plugged" : "open",
        () => BuildTapMesh(tesselator)
      ) is { } mesh
    )
      mesher.AddMeshData(mesh);

    // True suppresses the default block mesh, which draws the plug whatever the state.
    return true;
  }

  private MeshData? BuildTapMesh(ITesselatorAPI tesselator) {
    if (
      ExMeshCache.LoadShape(Api, ExMeshCache.ShapePathOf(Block))
      is not { } shape
    )
      return null;

    // Pruned rather than the engine's selectiveElements, whose per-segment prefix rule can keep or drop
    // the wrong subtree without reporting it.
    tesselator.TesselateShape(
      Block,
      ExShapeElements.Pruned(shape, IsPlugged ? PluggedElements : OpenElements),
      out MeshData mesh
    );
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }

  #endregion

  #region Serialization

  // The legacy key decides for a tap saved before the plug existed: it was drawn open whenever
  // `isPouring` was set, and reading a missing `plugged` as true would stop every open tap in an old
  // world at load. A tap saved since carries `plugged` and the fallback is never reached. The negation
  // is not a [Persist(Legacy:)] fallback - that reads the old key's value as-is - so it stays a Tree.
  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      "plugged",
      tree => tree.SetBool("plugged", IsPlugged),
      (tree, world) =>
        IsPlugged = tree.GetBool("plugged", !tree.GetBool("isPouring"))
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    bool prev = IsPlugged;
    base.FromTreeAttributes(tree, worldForResolving);
    // The mesh is the state, so a remote plug or unplug has to re-tesselate.
    if (Api?.Side == EnumAppSide.Client && prev != IsPlugged)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "iiex:tap-state",
        Lang.Get(IsPlugged ? "iiex:tap-closed" : "iiex:tap-open")
      )
    );

    // An open tap with nothing under its spout used to be impossible - the block refused to open one -
    // which is what made the blow-in ritual unbuildable. It opens now and delivers nothing, so the reason
    // has to be readable here or the tap is silently dead.
    if (!IsPlugged && !HasCanalBelow())
      dsc.AppendLine(Lang.Get("iiex:tap-err-nocanal"));

    // Resolve the furnace this tap drains and show its pool - metal from the lower tap, slag from the upper.
    // A tap with no furnace shows only the open/closed line above, and so does a tap on a furnace whose
    // drawing marks no tap: both Metal/SlagTapPos are null on a reverberatory hearth, which pours nothing.
    if (Anchor.Resolve() is { } core) {
      if (core.MetalTapPos is { } metal && SamePos(Pos, metal))
        core.AppendMoltenMetalInfo(dsc);
      else if (core.SlagTapPos is { } slag && SamePos(Pos, slag))
        core.AppendMoltenSlagInfo(dsc);
    }
  }

  // Coordinate-only equality: the tap and the core's tap cells share a dimension, so comparing X/Y/Z is
  // enough and side-steps BlockPos.Equals's dimension field.
  private static bool SamePos(BlockPos a, BlockPos b) =>
    a.X == b.X && a.Y == b.Y && a.Z == b.Z;

  #endregion

  #region Pouring

  /// <summary>
  /// Pushes <paramref name="moltenMetal"/> into the canal start beneath the spout.
  /// Returns the amount the canal actually consumed (0 if not pouring or nothing was transferred).
  /// </summary>
  public int TryPourMetal(ItemStack moltenMetal, float temperature) {
    if (IsPlugged || moltenMetal == null)
      return 0;

    if (SpoutPos() is not { } startPos)
      return 0;

    if (
      Api.World.BlockAccessor.GetBlockEntity(startPos)
      is not BlockEntityMoltenCanalStart startCanal
    )
      return 0;

    // The looser predicate, so a brim-full start still receives the pour and soaks
    // its heat (keeping it molten) instead of stalling and cooling.
    if (!startCanal.CanReceiveOrSoak(moltenMetal))
      return 0;

    startCanal.BeginFill(Pos.ToVec3d());

    // amount is passed by ref; ReceiveLiquidMetal decrements it by what the
    // network accepted, so after the call it holds the leftover, not the
    // consumed amount. The difference is what was actually accepted.
    int requested = moltenMetal.StackSize;
    int amount = requested;
    startCanal.ReceiveLiquidMetal(moltenMetal, ref amount, temperature);
    startCanal.OnPourOver();

    return requested - amount;
  }

  /// <summary>
  /// The cell this tap pours into: one step opposite its declared facing, then down. A tap in the east
  /// wall is declared <c>-w</c>, so it spouts east and out of the furnace. Null when the variant names no
  /// facing.
  /// </summary>
  private BlockPos? SpoutPos() {
    // ExOrientation.FacingFromSide, not BlockFacing.FromCode: FromCode returns null for a single-letter
    // side token, and a null facing here makes the tap return 0 and never pour, with no error logged.
    BlockFacing? facing = ExOrientation.FacingFromSide(Block.Variant["side"]);
    return facing == null ? null : Pos.AddCopy(facing.Opposite).DownCopy();
  }

  /// <summary>Whether a canal start stands under the spout. What the block info reports; not a gate on
  /// opening, which is the point of U4.8.</summary>
  private bool HasCanalBelow() =>
    SpoutPos() is { } pos
    && Api?.World.BlockAccessor.GetBlockEntity(pos)
      is BlockEntityMoltenCanalStart;

  #endregion
}
