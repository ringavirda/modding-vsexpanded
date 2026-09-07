using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Block entity behind <see cref="BlockChargePile"/>: a 16-band window onto one <see cref="ChargeColumn"/>
/// of the furnace whose shaft it stands in. It stores nothing of its own - bands, fill height and takes are
/// read live off the owning core's column - so it stays correct across destruction, reload and
/// re-materialisation. Extends <see cref="BlockEntityFurnacePart"/> for the cached anchor scan and the build
/// outline; the shape is static, so it draws through <see cref="OnTesselation"/> with no animation.
/// </summary>
[BlockEntityRegister]
public class BlockEntityChargePile : BlockEntityFurnacePart {
  // Main-thread reads go live to the column: core resolution is cached by the base and BandsAt is a short
  // walk, so the read cannot drift from what the block collides with. OnTesselation is the exception - it
  // runs off-thread, where walking the world for the anchor or enumerating ChargeColumn's segment list (a
  // List<T> the client clears and refills on every core sync) throws. The mesh therefore reads only the
  // snapshot published in #region Render snapshot.

  #region Reading the column

  /// <summary>
  /// The column this pile draws and which of its blocks this one is, or <c>(null, -1)</c> for an orphan: no
  /// core in range, a core that does not own this cell, or a cell outside the shaft box. The world-to-local
  /// conversion stays in the core (<c>ChargeColumnAt(BlockPos, out int)</c>); a pile rotating its own offset
  /// would agree with the core on a square shaft and disagree on any other.
  /// </summary>
  public (ChargeColumn? column, int blockIndex) Window {
    get {
      // Core is null on the client until the anchor scan resolves, and on a pile whose furnace was broken
      // out from under it. Both are ordinary states.
      if (Core is not { } core)
        return (null, -1);
      ChargeColumn? column = core.ChargeColumnAt(Pos, out int blockIndex);
      return column == null ? (null, -1) : (column, blockIndex);
    }
  }

  /// <summary>
  /// Ore-charge units per block, used as the quantum when no core is resolved yet. Must stay above 0:
  /// <see cref="ChargeColumn.BandsAt"/> clamps its quantum to at least 1, so a zero fallback puts every band
  /// boundary at 0 and the column draws as one empty stripe.
  /// </summary>
  private static int OreUnitsPerBlock =>
    IiexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock;

  /// <summary>Whether this pile is a live window onto a furnace's column. False for an orphan, which draws
  /// nothing and may be broken out.</summary>
  public bool BelongsToFurnace => Window.column != null;

  /// <summary>
  /// The bands this block draws, raceway end first, run-length encoded: bands
  /// <c>blockIndex*16 … (blockIndex+1)*16</c> of its column. Empty for an orphan and for a block above the
  /// stockline. Bands do not snap to block boundaries - a course taller than one block runs off the top of
  /// this block and across the bottom of the next as one stripe.
  /// </summary>
  public IReadOnlyList<ChargeBandRun> Bands {
    get {
      var (column, blockIndex) = Window;
      return column == null
        ? []
        : column.BandsAt(
          blockIndex,
          Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock
        );
    }
  }

  /// <summary>How far up the block the charge stands, 0..1. The collision and selection boxes follow
  /// it.</summary>
  public float FillHeight => BlockChargePile.HeightOf(Bands);

  /// <summary>
  /// Block-light value (0-24) from the hottest band this block draws
  /// (<see cref="ChargeColumn.PeakTemperature"/>): a block carries one light value and draws up to sixteen
  /// bands, so it takes the peak rather than an average and one white-hot band lights the block. Uses
  /// <see cref="MoltenMetal.GlowLevel"/>, shared with the canals, the barrel and the casting molds, so equal
  /// temperatures glow alike across the mod.
  /// </summary>
  public byte GlowLightLevel {
    get {
      var (column, blockIndex) = Window;
      if (column == null)
        return 0;
      int perBlock = Math.Max(1, Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock);
      return MoltenMetal.GlowLevel(
        column.PeakTemperature(blockIndex * perBlock, perBlock)
      );
    }
  }

  #endregion

  #region Taking by hand

  /// <summary>
  /// Lifts one band off the top of the column and returns it as a stack, or null when there is nothing to
  /// take: an orphan, an empty column, or a material whose item no longer resolves. The top of the column,
  /// not of this block, so a take undoes the last load whichever window it was clicked through. Clamped to
  /// the top segment as well as to one band, so it returns a single material; the material is resolved
  /// before anything is removed, so a column holding an unresolvable code is not emptied into nothing.
  /// </summary>
  public ItemStack? TryTakeTop() {
    var (column, _) = Window;
    if (column is not { Segments.Count: > 0 })
      return null;

    ChargeSegment top = column.Segments[^1];
    // One band's worth off the top, in whatever unit this furnace measures its charge in: items for an ore
    // pile, metal units for a remelt one. Derived from the per-block quantum so the two stay in step.
    int perBand = Math.Max(
      1,
      (Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock)
        / ChargeColumn.BandsPerBlock
    );
    int units = Math.Min(perBand, top.Units);

    ItemStack? stack = StackOf(top.Material, units);
    if (stack == null)
      return null;

    column.TakeTop(units);
    if (top.Mix.HasContent)
      Burden.Write(stack, top.Mix);

    OnColumnChanged();
    Core?.MarkDirty(true); // the shaft total moved and the hopper reads it off the core
    return stack;
  }

  /// <summary>
  /// Splices this block's own units out of the column and returns them as stacks - what breaking a charge
  /// pile out of the shaft wall does, and the only way to reach a chill, which sits at the bottom of the
  /// shaft where <see cref="TryTakeTop"/> cannot get at it. See docs/design/layered-charge.md. Grouped by
  /// material and burden grade, a stack carrying one burden mix. Order is raceway-first; a material whose
  /// item no longer resolves is pushed back into the column rather than destroyed.
  /// </summary>
  public List<ItemStack> TakeWindow() {
    var drops = new List<ItemStack>();
    var (column, blockIndex) = Window;
    if (column == null || blockIndex < 0)
      return drops;

    int perBlock = Math.Max(1, Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock);
    List<ChargeSegment> taken = column.TakeSpan(
      blockIndex * perBlock,
      perBlock
    );
    if (taken.Count == 0)
      return drops;

    // (material, mix) -> index of the stack already accumulating for it. The List holds the raceway-first
    // order the bands stood in, which a Dictionary alone would not preserve.
    var seen = new Dictionary<(string, BurdenMix), int>();
    var unresolved = new List<ChargeSegment>();

    foreach (ChargeSegment segment in taken) {
      var key = (segment.Material, segment.Mix);
      if (seen.TryGetValue(key, out int at)) {
        drops[at].StackSize += segment.Units;
        continue;
      }

      ItemStack? stack = StackOf(segment.Material, segment.Units);
      if (stack == null) {
        unresolved.Add(segment);
        continue;
      }

      if (segment.Mix.HasContent)
        Burden.Write(stack, segment.Mix);
      seen[key] = drops.Count;
      drops.Add(stack);
    }

    // Anything that could not be resolved to an item goes back into the column rather than evaporating.
    foreach (ChargeSegment segment in unresolved)
      column.Push(
        segment.Material,
        segment.Units,
        segment.Temperature,
        segment.Mix
      );

    OnColumnChanged();
    Core?.MarkDirty(true); // the shaft total moved and the hopper reads it off the core
    return drops;
  }

  /// <summary>
  /// Asks this pile's furnace to re-materialise its charge wall. Must run after the broken block is gone: a
  /// splice shortens the column by one block, and a sync run while the block still stands removes the
  /// topmost pile instead. Safe on a block entity the world has already unregistered - the anchor link
  /// resolves against the world by position, and the caller holds this object alive across the break.
  /// </summary>
  public void ResyncOwner() => Core?.SyncChargeBlocks();

  /// <summary>Resolves a segment's material code to a stack, trying items before blocks. The shaft holds
  /// items today, and the code is stored as a plain string so the set stays open.</summary>
  private ItemStack? StackOf(string material, int units) {
    if (Api?.World is not { } world || string.IsNullOrEmpty(material))
      return null;

    var code = new AssetLocation(material);
    if (world.GetItem(code) is { } item)
      return new ItemStack(item, units);
    if (world.GetBlock(code) is { } block)
      return new ItemStack(block, units);
    return null;
  }

  #endregion

  #region Render snapshot

  // Delay, in milliseconds, before the client re-reads its column once after load. A pile's core can finish
  // loading between this block entity's Initialize and its chunk being tesselated - the two are unordered
  // when a furnace straddles a chunk boundary - and no event covers that gap. Not config.
  private const int LoadSettleMs = 500;

  private BlockChargePile.ChargeBandSlab[] _renderSlabs = [];

  /// <summary>
  /// The stripes the mesh draws: an immutable snapshot of the column as of the last main-thread report, and
  /// the only state <see cref="OnTesselation"/> may read. Public so tests can assert it refreshes.
  /// </summary>
  public IReadOnlyList<BlockChargePile.ChargeBandSlab> RenderSlabs =>
    _renderSlabs;

  /// <summary>
  /// Re-reads the column and republishes what the mesh may see. Main thread only: it walks the world for the
  /// anchor and enumerates the column. The array is assigned whole, so a tesselation already in flight sees
  /// one complete snapshot or the other, never one being refilled underneath it.
  /// </summary>
  private void SnapshotBands() =>
    _renderSlabs = [.. BlockChargePile.SlabsOf(Bands)];

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // Only the client draws, so only the client needs a snapshot.
    if (api.Side != EnumAppSide.Client)
      return;
    SnapshotBands();
    // Fires once rather than polling. See LoadSettleMs.
    RegisterDelayedCallback(_ => RefreshIfChanged(), LoadSettleMs);
  }

  /// <summary>Re-snapshots and redraws only when the result changed, so the settle pass costs a chunk
  /// re-tesselation only on the piles whose core arrived late.</summary>
  private void RefreshIfChanged() {
    BlockChargePile.ChargeBandSlab[] before = _renderSlabs;
    SnapshotBands();
    if (!before.AsSpan().SequenceEqual(_renderSlabs))
      Api?.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Redraw

  /// <summary>
  /// Called by the furnace when it has changed this pile's column - a charge laid, a descent step, a take.
  /// The core owns the column, so the only route in is <c>SyncChargeBlocks</c>; a column mutator that skips
  /// it leaves the client drawing a stale shaft. Republishes the snapshot the mesh reads, syncs the block
  /// entity, and client-side requests the re-tesselation that consumes the new snapshot. Sends nothing when
  /// neither the stripes nor the glow level changed, the counter-current pass syncing every second.
  /// </summary>
  public void OnColumnChanged() {
    BlockChargePile.ChargeBandSlab[] before = _renderSlabs;
    byte glowBefore = _lastGlow;
    SnapshotBands();
    _lastGlow = GlowLightLevel;

    if (glowBefore == _lastGlow && before.AsSpan().SequenceEqual(_renderSlabs))
      return;

    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>Glow level the last redraw published, so a temperature move that does not cross a level costs
  /// nothing. Not saved: re-derived on the first <see cref="OnColumnChanged"/> after a load.</summary>
  private byte _lastGlow;

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    // The pile writes nothing of its own; the packet only tells the client its column moved. Api is null
    // here at chunk load (the engine deserialises before Initialize), so this branch covers the live sync
    // only and Initialize takes the load-time snapshot.
    if (Api?.Side != EnumAppSide.Client)
      return;
    SnapshotBands();
    Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Render

  /// <summary>
  /// Draws the bands as stacked slabs: one tesselation of the shape's per-material element per run, scaled
  /// to the run's height and lifted onto the runs below it. Runs on the tesselation thread, so it reads
  /// <see cref="RenderSlabs"/> and nothing else - not the column, not <c>Core</c>. Always returns
  /// <c>true</c>, including when it drew nothing, because the block's default mesh is a full-height cube of
  /// every material at once, which an orphan must not fall back to.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Api is not ICoreClientAPI capi)
      return true;

    // Read once into a local: the main thread can replace the field mid-draw, and a run must finish on the
    // snapshot it started on.
    BlockChargePile.ChargeBandSlab[] slabs = _renderSlabs;
    if (slabs.Length == 0)
      return true;

    foreach (BlockChargePile.ChargeBandSlab slab in slabs) {
      float height = slab.ToY - slab.FromY;
      if (height <= 0f)
        continue;

      // The element name is the whole key: the pruned shape is a function of it alone, and height is
      // applied by Scale below, after tesselation. That holds only because each material is its own
      // shape element. The same local feeds both the key and the prune, so they cannot drift.
      string element = BlockChargePile.ElementOf(slab.Material);
      MeshData? unscaled = ExMeshCache.GetOrCreate(
        capi,
        Block,
        element,
        () => {
          Shape? shape = ExMeshCache.LoadShape(
            capi,
            ExMeshCache.ShapePathOf(Block)
          );
          if (shape == null)
            return null;
          tesselator.TesselateShape(
            "chargepile",
            ExShapeElements.Pruned(shape, [element]),
            out MeshData built,
            capi.Tesselator.GetTextureSource(Block)
          );
          return built;
        }
      );
      if (unscaled == null)
        continue;

      // Clone before shaping it. Scale and Translate mutate in place, and the cached mesh is shared by
      // every pile in the world - scaling it directly would compound on every draw and drag every other
      // pile's bands with it.
      MeshData mesh = unscaled.Clone();
      // Scaled about the block floor (y = 0), not its centre: band positions are measured from the floor
      // up, and scaling about the centre would leave every partial slab floating.
      mesh.Scale(new Vec3f(0.5f, 0f, 0.5f), 1f, height, 1f);
      mesh.Translate(0f, slab.FromY, 0f);
      mesher.AddMeshData(mesh);
    }
    return true;
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    // Only the orphan case. What the shaft holds is reported once by the hopper's slice of the furnace HUD
    // rather than repeated on every charge block.
    if (!BelongsToFurnace)
      dsc.AppendLine(Lang.Get("iiex:furnacepart-nofurnace"));
  }

  #endregion
}
