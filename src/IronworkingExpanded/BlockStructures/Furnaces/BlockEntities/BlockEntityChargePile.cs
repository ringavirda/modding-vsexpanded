using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The block entity behind <see cref="BlockChargePile"/>: a 16-band window onto one
/// <see cref="ChargeColumn"/> of the furnace above whose shaft it stands.
/// <para>
/// <b>It stores nothing.</b> Not one attribute rides its save tree - everything it draws, collides with
/// and hands to a player is read live off its owning core's column, so a pile can be destroyed, reloaded,
/// or materialised fresh by the furnace and be instantly correct. That is what makes breaking one safe,
/// and it is why this does not derive <c>BlockEntityItemPile</c>: there is no inventory to lose.
/// </para>
/// <para>
/// It extends <see cref="BlockEntityFurnacePart"/> for the one thing it genuinely shares with the doors,
/// the hearths and the taps - finding the furnace it belongs to through a cached, throttled anchor scan,
/// and routing the ctrl+shift build outline with it. Like the hearths it has <b>no animations</b>: the
/// shape is static and only its content changes, so it draws through <see cref="OnTesselation"/> and
/// <c>ApplyPose</c> stays the base's no-op.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityChargePile : BlockEntityFurnacePart
{
  // Everything the main thread reads - the bands, the fill height, a take - is read live. Core resolution
  // is already cached and throttled by the base and BandsAt is a 16-step walk over a handful of segments,
  // so it costs almost nothing and removes the whole class of "the block draws one thing and collides with
  // another" that a cache invalidated by the wrong event creates.
  //
  // The tesselation thread is the exception, and it is not a preference. `OnTesselation` runs on its own
  // thread; reading live from there means walking the world for the anchor (which writes the link's cached
  // position, and can drive the core's SetStructureAngle into rewriting _structure/_currentAngle/_facings)
  // and then enumerating ChargeColumn's segment list - a List<T> the client clears and refills on every
  // core sync, i.e. exactly when a charging furnace is busiest. A foreach over a list another thread is
  // refilling throws InvalidOperationException. So the mesh reads a snapshot and nothing else; see
  // #region Render snapshot.

  #region Reading the column

  /// <summary>
  /// The column this pile draws and which of its blocks this one is, or <c>(null, -1)</c> when the pile is
  /// an orphan - no core in range, a core that does not own this cell, or a cell outside the shaft box.
  /// <para>
  /// The whole world-to-local conversion is the core's (<c>ChargeColumnAt(BlockPos, out int)</c>), not
  /// reimplemented here. A pile that rotated its own offset would be right on a square shaft at every
  /// facing and wrong on any other, because a 3x3 column set is closed under 90 deg rotation.
  /// </para>
  /// </summary>
  public (ChargeColumn? column, int blockIndex) Window
  {
    get
    {
      // Core is null on the client until the anchor scan resolves, and stays null forever on a pile whose
      // furnace was broken out from under it. Both are ordinary states, not errors.
      if (Core is not { } core)
        return (null, -1);
      ChargeColumn? column = core.ChargeColumnAt(Pos, out int blockIndex);
      return column == null ? (null, -1) : (column, blockIndex);
    }
  }

  /// <summary>Whether this pile is a live window onto a furnace's column - false for an orphan, which is
  /// what decides both that it draws nothing and that it may be broken out.</summary>
  /// <summary>
  /// The ore-charge quantum, used when this pile has no resolved core yet - on the client before the
  /// anchor scan, and in any fixture that stands a pile up on its own.
  /// It must not be 0: <see cref="ChargeColumn.BandsAt"/> clamps its quantum to at least 1, so a zero
  /// fallback puts every band boundary at 0 and the whole column draws as one empty stripe.
  /// </summary>
  private static int OreUnitsPerBlock =>
    IwexValues.ChargeItemsPerBand * ChargeColumn.BandsPerBlock;

  public bool BelongsToFurnace => Window.column != null;

  /// <summary>
  /// The bands this block draws, raceway end first, run-length encoded - bands
  /// <c>blockIndex*16 … (blockIndex+1)*16</c> of its column. Empty for an orphan and for a block standing
  /// above the stockline.
  /// <para>
  /// <b>Bands do not snap to block boundaries.</b> The window is cut out of the column's own continuous
  /// band sequence, so a course taller than one block runs off the top of this block and straight on
  /// across the bottom of the next as one stripe - which is what a furnace charge looks like in section.
  /// </para>
  /// </summary>
  public IReadOnlyList<ChargeBandRun> Bands
  {
    get
    {
      var (column, blockIndex) = Window;
      return column == null
        ? []
        : column.BandsAt(blockIndex, Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock);
    }
  }

  /// <summary>How far up the block the charge stands, 0..1 - what the collision and selection boxes
  /// follow.</summary>
  public float FillHeight => BlockChargePile.HeightOf(Bands);

  /// <summary>
  /// Block-light value (0-24) from the <b>hottest band this block draws</b>, so a charged shaft is
  /// visibly incandescent at the raceway and dark at the stockline - the counter-current temperature
  /// profile, read straight off the wall.
  /// <para>
  /// <b>A block carries one light value and draws up to sixteen bands</b>, so it takes the hottest
  /// rather than an average: a block holding one white-hot band should look lit. See
  /// <see cref="ChargeColumn.PeakTemperature"/>.
  /// </para>
  /// <para>
  /// Shares <see cref="MoltenMetal.GlowLevel"/> with every other hot thing in the mod - the molten
  /// canals, the barrel, the casting molds - so a shaft at 1 200 °C and iron at 1 200 °C glow alike.
  /// </para>
  /// </summary>
  public byte GlowLightLevel
  {
    get
    {
      var (column, blockIndex) = Window;
      if (column == null)
        return 0;
      int perBlock = Math.Max(
        1,
        Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock
      );
      return MoltenMetal.GlowLevel(
        column.PeakTemperature(blockIndex * perBlock, perBlock)
      );
    }
  }

  #endregion

  #region Taking by hand

  /// <summary>
  /// Lifts one band off the <b>top of the column</b> and returns it as a stack, or null when there is
  /// nothing to take (an orphan, an empty column, or a material whose item no longer resolves).
  /// <para>
  /// <b>The top of the column, not of this block.</b> A column is one continuous stack and the pile is a
  /// window onto it; the stockline is the only end a player can reach, and it is the end
  /// <see cref="ChargeColumn.Push"/> lays on - so a take is exactly an undo of the last load, whichever
  /// window it was clicked through.
  /// </para>
  /// <para>
  /// The take is clamped to the <b>top segment</b> as well as to one band, so it always hands back a
  /// single material: a take that spanned a course boundary would owe the player two stacks for one click.
  /// And the material is resolved <b>before</b> anything is removed - a column holding a code no item
  /// answers to must not be emptied into nothing.
  /// </para>
  /// <para>
  /// Adding by hand is <b>not</b> here: it needs the band-order rule (coke only above the last burden,
  /// lowest columns first), which is Task 3.2's.
  /// </para>
  /// </summary>
  public ItemStack? TryTakeTop()
  {
    var (column, _) = Window;
    if (column is not { Segments.Count: > 0 })
      return null;

    ChargeSegment top = column.Segments[^1];
    // One band's worth off the top, in whatever this furnace measures its charge in - items for an ore
    // pile, metal units for a remelt one. Derived from the per-block quantum so the two stay in step.
    int perBand = Math.Max(
      1,
      (Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock) / ChargeColumn.BandsPerBlock
    );
    int units = Math.Min(perBand, top.Units);

    ItemStack? stack = StackOf(top.Material, units);
    if (stack == null)
      return null;

    column.TakeTop(units);
    if (top.Mix.HasContent)
      Burden.Write(stack, top.Mix);

    OnColumnChanged();
    Core?.MarkDirty(true); // the shaft total moved, and the hopper's readout is the core's
    return stack;
  }

  /// <summary>
  /// <b>Splices this block's own units out of the column and hands them back as stacks</b> - what
  /// breaking a charge pile out of the shaft wall does, and the whole of the R2 recoverability promise for
  /// a chilled furnace.
  /// <para>
  /// <b>This is why a mid-span take had to exist.</b> A chill sits at the <em>bottom</em> of the shaft by
  /// definition, so <see cref="TryTakeTop"/> - the only other way a player can reach a column - leaves the
  /// exact failure recoverability exists for unrecoverable. <c>layered-charge.md</c> settles it twice:
  /// breaking splices, and everything above falls. Task 2.3 had refused the break precisely because
  /// <see cref="ChargeColumn"/> could not express this; <see cref="ChargeColumn.TakeSpan"/> is that
  /// reversal.
  /// </para>
  /// <para>
  /// <b>Grouped by material <em>and grade</em>, not by material alone.</b> The design says "one stack per
  /// material" and means "not one stack per segment" - but a stack carries <b>one</b> burden mix, so two
  /// courses of different grade in one window cannot honestly become one stack: merging them would have to
  /// invent a grade the player never made, and averaging two discrete grades lands between both. It is the
  /// same rule the hoppers already enforce when they refuse two grades into one tank. In the ordinary case -
  /// a window of coke over one grade of burden - this <em>is</em> one stack per material.
  /// </para>
  /// <para>
  /// Order is raceway-first, so the drops come out in the order the bands stood. A material whose item no
  /// longer resolves is <b>left in the column</b> rather than silently destroyed, exactly as
  /// <see cref="TryTakeTop"/> refuses to empty a column into nothing - the units stay put and fall with the
  /// rest.
  /// </para>
  /// </summary>
  public List<ItemStack> TakeWindow()
  {
    var drops = new List<ItemStack>();
    var (column, blockIndex) = Window;
    if (column == null || blockIndex < 0)
      return drops;

    int perBlock = Math.Max(1, Core?.ChargeUnitsPerBlock ?? OreUnitsPerBlock);
    List<ChargeSegment> taken = column.TakeSpan(blockIndex * perBlock, perBlock);
    if (taken.Count == 0)
      return drops;

    // (material, mix) -> the index of the stack already accumulating for it. A List keeps the raceway-first
    // order the bands stood in; a Dictionary alone would not.
    var seen = new Dictionary<(string, BurdenMix), int>();
    var unresolved = new List<ChargeSegment>();

    foreach (ChargeSegment segment in taken)
    {
      var key = (segment.Material, segment.Mix);
      if (seen.TryGetValue(key, out int at))
      {
        drops[at].StackSize += segment.Units;
        continue;
      }

      ItemStack? stack = StackOf(segment.Material, segment.Units);
      if (stack == null)
      {
        unresolved.Add(segment);
        continue;
      }

      if (segment.Mix.HasContent)
        Burden.Write(stack, segment.Mix);
      seen[key] = drops.Count;
      drops.Add(stack);
    }

    // Anything that could not be resolved to an item goes back where it was rather than evaporating.
    foreach (ChargeSegment segment in unresolved)
      column.Push(segment.Material, segment.Units, segment.Temperature, segment.Mix);

    OnColumnChanged();
    Core?.MarkDirty(true); // the shaft total moved, and the hopper's readout is the core's
    return drops;
  }

  /// <summary>
  /// Asks this pile's furnace to re-materialise its charge wall.
  /// <para>
  /// <b>It must be called after the block is gone, which is why it is a method and not part of
  /// <see cref="TakeWindow"/>.</b> A splice makes the column one block shorter, so the sync's job is to drop
  /// the now-empty pile off the top - and if it ran while the broken block was still standing it would take
  /// the top one away and then the break would punch a hole in the middle, leaving the wall a block short in
  /// the wrong place. Run in the other order there is no hole at all: the block comes straight back if the
  /// shortened column still reaches that height.
  /// </para>
  /// <para>
  /// Safe on a block entity the world has already unregistered - the anchor link is resolved against the
  /// world by position, and the caller holds this object alive across the break.
  /// </para>
  /// </summary>
  public void ResyncOwner() => Core?.SyncChargeBlocks();

  /// <summary>Resolves a segment's material code to a stack. Items first, then blocks: the shaft holds
  /// items (burden, coke) today, but the code is stored as a plain code string precisely so the set is not
  /// closed.</summary>
  private ItemStack? StackOf(string material, int units)
  {
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

  // How long after load the client re-reads its column once. A pile's core can finish loading in the
  // window between this block entity's Initialize and its chunk being tesselated - the two are not
  // ordered when a furnace straddles a chunk boundary - and there is no event on the far side of that to
  // hang a redraw off. Not a tuning knob and deliberately not config: it is "the next moment the world
  // has settled", the same shape of constant as ChargeColumn.BandsPerBlock.
  private const int LoadSettleMs = 500;

  private BlockChargePile.ChargeBandSlab[] _renderSlabs = [];

  /// <summary>
  /// The stripes the mesh draws - an <b>immutable snapshot</b> of what the column held the last time
  /// something on the main thread said it had moved, and the only state
  /// <see cref="OnTesselation"/> is allowed to read.
  /// <para>
  /// Exposed so the seam can be asserted: a snapshot that never refreshes and a live read are
  /// indistinguishable from the outside until one of them is stale.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockChargePile.ChargeBandSlab> RenderSlabs => _renderSlabs;

  /// <summary>
  /// Re-reads the column and republishes what the mesh may see. <b>Main thread only</b> - it walks the
  /// world for the anchor and enumerates the column.
  /// <para>
  /// The array is built first and assigned whole. A reference assignment is atomic, so a tesselation
  /// already in flight sees either the previous snapshot or this one, complete - never a list being
  /// refilled underneath it, which is the throw this whole arrangement exists to remove.
  /// </para>
  /// </summary>
  private void SnapshotBands() => _renderSlabs = [.. BlockChargePile.SlabsOf(Bands)];

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // Only the client draws, so only the client needs a snapshot. Taking one server-side would walk the
    // world for an anchor once per block of every shaft at chunk load, to fill a field nothing reads.
    if (api.Side != EnumAppSide.Client)
      return;
    SnapshotBands();
    // Once, not a poll. See LoadSettleMs: this closes the load-order race, nothing more.
    RegisterDelayedCallback(_ => RefreshIfChanged(), LoadSettleMs);
  }

  /// <summary>Re-snapshots and redraws only when the answer actually moved, so the settle pass costs a
  /// chunk re-tesselation on the piles whose core arrived late and nothing on the rest.</summary>
  private void RefreshIfChanged()
  {
    BlockChargePile.ChargeBandSlab[] before = _renderSlabs;
    SnapshotBands();
    if (!before.AsSpan().SequenceEqual(_renderSlabs))
      Api?.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Redraw

  /// <summary>
  /// What the furnace calls when it has changed this pile's column - a charge laid, a descent step, a
  /// take. The core owns the column, so nothing here can notice on its own and polling every tick to find
  /// out would cost a shaft-full of block entities a walk per second.
  /// <para>
  /// It is deliberately <b>not wired into any tick</b> - it is called from <c>SyncChargeBlocks</c>,
  /// which charging, descent and burn-out all end with. That is the only redraw route there is, so a
  /// future column mutator that forgets it leaves the client drawing a permanently stale shaft with no
  /// server-side assertion able to see it.
  /// </para>
  /// <para>
  /// Three things, and all three are needed: republish the snapshot the mesh reads (nothing else may do
  /// that, because the tesselation thread must not go looking), sync the block entity so the client learns
  /// the column moved at all, and - client-side - ask for the re-tesselation that will consume the new
  /// snapshot. The sync/redraw pair is the furnace family's existing route, the same two lines the heating
  /// hearth uses when its rows change; it is not a new packet.
  /// </para>
  /// <para>
  /// <b>It sends nothing when the picture is unchanged, and that is load-bearing rather than an
  /// optimisation.</b> The counter-current pass moves every band's temperature <em>every second</em> and
  /// ends with a <c>SyncChargeBlocks</c>, so an unconditional <see cref="BlockEntity.MarkDirty"/> here is
  /// a packet per charge block per second - thirty-six a second for one blast furnace, and every furnace
  /// in the world at once. Comparing first means the wire carries a change only when a stripe moved or the
  /// glow crossed a level, which is exactly when a player could see one.
  /// </para>
  /// </summary>
  public void OnColumnChanged()
  {
    BlockChargePile.ChargeBandSlab[] before = _renderSlabs;
    byte glowBefore = _lastGlow;
    SnapshotBands();
    _lastGlow = GlowLightLevel;

    if (
      glowBefore == _lastGlow
      && before.AsSpan().SequenceEqual(_renderSlabs)
    )
      return;

    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  /// <summary>Glow level the last redraw published, so a temperature move that does not cross a level
  /// costs nothing. Not saved: it is re-derived from the column on the first
  /// <see cref="OnColumnChanged"/> after a load, and a stale 0 only means one extra packet.</summary>
  private byte _lastGlow;

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    // The pile writes nothing of its own; the packet's only job is to tell the client its column moved.
    // Api is null here at chunk load (the engine deserialises before Initialize), which is why the
    // snapshot is taken in Initialize as well - this branch only covers the live sync.
    if (Api?.Side != EnumAppSide.Client)
      return;
    SnapshotBands();
    Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Render

  /// <summary>
  /// Draws the bands as stacked slabs - one tesselation of the shape's per-material element per run,
  /// scaled to the run's height and lifted onto the runs below it.
  /// <para>
  /// <b>Runs on the tesselation thread, so it reads <see cref="RenderSlabs"/> and nothing else.</b> Not
  /// the column, not the core, not <c>Core</c> itself - resolving the anchor writes to the link's cache and
  /// can make the core reload its own structure, and walking the column enumerates a list the client
  /// clears and refills on every core sync. See the note at the top of this class.
  /// </para>
  /// <para>
  /// The shape is a single full-height cube per material, so scaling it by <c>bands/16</c> about the block
  /// floor makes the slabs tile the block exactly and a course that spans two blocks joins with no seam.
  /// The element is pruned rather than left to the engine's <c>selectiveElements</c> matching, whose
  /// per-segment prefix rule has silently kept the wrong subtree in this mod before.
  /// </para>
  /// <para>
  /// Returns <c>true</c> whatever it drew, including nothing: the block's own default mesh is a full cube
  /// of <b>every</b> material at once at full height, which is never what a pile looks like - and an orphan
  /// must render as nothing at all rather than as a solid block of coke.
  /// </para>
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  )
  {
    if (Api is not ICoreClientAPI capi)
      return true;

    // Read once into a local: the field can be replaced by the main thread mid-draw, and a run that
    // started on one snapshot must finish on it rather than half on each.
    BlockChargePile.ChargeBandSlab[] slabs = _renderSlabs;
    if (slabs.Length == 0)
      return true;

    Shape? shape = capi
      .Assets.TryGet(
        Block.Shape.Base.Clone()
          .WithPathPrefixOnce("shapes/")
          .WithPathAppendixOnce(".json")
      )
      ?.ToObject<Shape>();
    if (shape == null)
      return true;

    foreach (BlockChargePile.ChargeBandSlab slab in slabs)
    {
      float height = slab.ToY - slab.FromY;
      if (height <= 0f)
        continue;

      // The cache key must name every input to the mesh, and here the element is the only one: the
      // pruned shape is a function of this string and nothing else, and the height comes from Scale below,
      // after tesselation. That holds only because each material is its own shape element. Draw a second
      // material by `ExShapeElements.Retextured`-ing one element instead - the cheaper route, and the one
      // BlockFirebox takes - and coke and charcoal collide on this one key, whichever tesselated first wins
      // for both, and a whole shaft of charcoal renders as coke with nothing in the mesh path looking wrong.
      // BlockEntityFirebox pays for its retexture by keying on layers and texture; this pays by keying on
      // an element that already encodes the texture. One local, so the key and the prune cannot drift apart.
      string element = BlockChargePile.ElementOf(slab.Material);
      tesselator.TesselateShape(
        "chargepile-" + element,
        ExShapeElements.Pruned(shape, [element]),
        out MeshData mesh,
        capi.Tesselator.GetTextureSource(Block)
      );
      // Scaled about the block floor (y = 0), not its centre: a band's place in the column is measured
      // from the floor up, and scaling about the centre would leave every partial slab floating.
      mesh.Scale(new Vec3f(0.5f, 0f, 0.5f), 1f, height, 1f);
      mesh.Translate(0f, slab.FromY, 0f);
      mesher.AddMeshData(mesh);
    }
    return true;
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    // Only the orphan case. What the shaft holds is the hopper's slice of the furnace HUD by design -
    // repeating it on 36 blocks would say the same number 36 times.
    if (!BelongsToFurnace)
      dsc.AppendLine(Lang.Get("iwex:furnacepart-nofurnace"));
  }

  #endregion
}
