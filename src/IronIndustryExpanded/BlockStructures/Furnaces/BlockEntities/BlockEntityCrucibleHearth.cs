using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The crucible furnace's hearth: a fuel bed that also holds four pots. Everything about the bed is
/// <see cref="BlockEntityFirebox"/>'s and is inherited unchanged; what is added here is the four holes -
/// seating a pot, charging it with crushed blister steel, bringing it up gently, melting it, and handing
/// back a pourable pot at the end. See docs/design/machines/crucible-furnace.md.
/// </summary>
/// <remarks>
/// The holes are sub-block: one cell holds all four, so which hole a gesture lands on is decided by the
/// hearth rather than by which cell was clicked. The rule is the first hole that can take the gesture, in
/// element order - the same order the shape numbers them.
/// </remarks>
[BlockEntityRegister]
public class BlockEntityCrucibleHearth : BlockEntityFirebox {
  private readonly CrucibleHole[] _holes =
  [
    .. Enumerable
      .Range(0, CrucibleHearthLayout.Holes)
      .Select(_ => new CrucibleHole()),
  ];

  /// <summary>The four holes, in the order the shape numbers them.</summary>
  public IReadOnlyList<CrucibleHole> Holes => _holes;

  #region Gestures

  /// <summary>
  /// Stands <paramref name="pot"/> in the first free hole, carrying its age across. Returns false when
  /// every hole is taken or the stack is not a burned pot.
  /// </summary>
  public bool Seat(ItemStack? pot) {
    if (pot?.Block is not BlockSteelCrucible)
      return false;
    if (pot.Collectible.Code?.Path != BlockSteelCrucible.BurnedCode)
      return false;

    CrucibleHole? free = _holes.FirstOrDefault(h => !h.Occupied);
    if (free?.Seat(CrucibleFiring.Of(pot)) != true)
      return false;

    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Feeds crushed blister steel into the first pot that will take it, and returns how many whole items
  /// were consumed. Sized in items rather than units so the caller can take exactly that many out of the
  /// player's hand.
  /// </summary>
  public int Charge(ItemStack? blister) {
    int perItem = UnitsPerItem(blister);
    if (perItem <= 0)
      return 0;

    int consumed = 0;
    foreach (CrucibleHole hole in _holes) {
      while (consumed < blister!.StackSize && hole.TakeCharge(perItem) > 0)
        consumed++;
      if (consumed >= blister.StackSize)
        break;
    }
    if (consumed > 0)
      MarkDirty(true);
    return consumed;
  }

  /// <summary>
  /// Pulls the first hole that has anything to give: a finished pot before an unfinished one, so an
  /// empty-handed player takes the heat out rather than dismantling the charge behind it.
  /// </summary>
  /// <remarks>
  /// A part-charged pot comes back with its blister steel beside it rather than inside it - the pot the
  /// player gets is the burned one they seated, at the age they seated it, and the charge is loose items
  /// again. Returns null when every hole is empty, which is what lets the firebox's own fuel gesture
  /// through underneath.
  /// </remarks>
  public ItemStack? Pull() {
    CrucibleHole? hole =
      _holes.FirstOrDefault(h => h.Molten)
      ?? _holes.FirstOrDefault(h => h.Occupied);
    if (hole is null || Api is null)
      return null;

    ItemStack? pulled = hole.Molten
      ? CruciblePot.Molten(Api!.World, hole.Firings, hole.Metal)
      : CruciblePot.Burned(Api!.World, hole.Firings);
    if (pulled is null)
      return null;

    if (!hole.Molten && hole.Charge > 0)
      Spill(hole.Charge, up: true);

    hole.Clear();
    MarkDirty(true);
    return pulled;
  }

  #endregion

  #region The heat

  /// <summary>
  /// One tick of the furnace's fire on the holes. With the damper shut the pots come up gently; with it
  /// open they take the full draught, which is what melts a preheated pot and destroys one that is still
  /// coming up.
  /// </summary>
  /// <remarks>
  /// The crack is checked here rather than in the melt cycle because the melt cycle only runs once the
  /// chamber is at process temperature, and a furnace with its damper shut never gets there - so a pot
  /// taken into the full fire too early would never be looked at.
  /// </remarks>
  public void FireTick(float dt, bool damperOpen) {
    bool cracked = false;
    foreach (CrucibleHole hole in _holes) {
      if (!damperOpen) {
        hole.SoakTick(dt);
        continue;
      }
      if (!hole.CracksInFullDraught)
        continue;

      int spilled = hole.Crack();
      cracked = true;
      if (spilled > 0)
        Spill(spilled, up: false);
    }

    if (cracked && Api?.Side == EnumAppSide.Server)
      Api.World.PlaySoundAt(
        new AssetLocation("game:sounds/block/ceramicbreak"),
        Pos.X + 0.5,
        Pos.Y + 0.5,
        Pos.Z + 0.5
      );
    MarkDirty(true);
  }

  /// <summary>
  /// One melt cycle: every charged, preheated pot advances by <paramref name="seconds"/>, and each one
  /// that crosses the line becomes finished metal. Four pots are four independent heats, so a pot seated
  /// late finishes late rather than riding the others' progress.
  /// </summary>
  public int MeltStep(float seconds) {
    int finished = _holes.Count(h => h.MeltStep(seconds));
    MarkDirty(true);
    return finished;
  }

  #endregion

  #region Items

  /// <summary>
  /// Material units one of <paramref name="stack"/> is worth to a pot, or zero if it is not blister steel.
  /// Read off the shipped <c>materialUnits</c> for our own chunk and stated for vanilla's bit, which
  /// carries none.
  /// </summary>
  /// <summary>Whether <paramref name="stack"/> is something a pot takes as charge.</summary>
  public static bool IsBlister(ItemStack? stack) => UnitsPerItem(stack) > 0;

  private static int UnitsPerItem(ItemStack? stack) {
    string? code = stack?.Collectible?.Code?.ToString();
    if (code == BlisterBreaking.ChunkCode)
      return BlisterBreaking.ChunkUnits;
    if (code == BlisterBreaking.BitCode)
      return BlisterBreaking.BitUnits;
    return 0;
  }

  /// <summary>
  /// Drops <paramref name="units"/> of blister steel as chunks and bits - into the ash pit below when a pot
  /// cracked, at the hearth's own mouth when a charge was simply taken back out.
  /// </summary>
  private void Spill(int units, bool up) {
    if (Api?.Side != EnumAppSide.Server)
      return;

    int chunks = units / BlisterBreaking.ChunkUnits;
    int bits =
      (units - chunks * BlisterBreaking.ChunkUnits) / BlisterBreaking.BitUnits;
    double y = up ? Pos.Y + 1.0 : Pos.Y - 1.0;

    foreach (
      (string code, int count) in new[]
      {
        (BlisterBreaking.ChunkCode, chunks),
        (BlisterBreaking.BitCode, bits),
      }
    ) {
      if (count <= 0)
        continue;
      Item? item = Api.World.GetItem(new AssetLocation(code));
      if (item != null)
        Api.World.SpawnItemEntity(
          new ItemStack(item, count),
          new Vintagestory.API.MathTools.Vec3d(Pos.X + 0.5, y, Pos.Z + 0.5)
        );
    }
  }

  #endregion

  #region Render

  private string _renderHoles = string.Empty;

  protected override void Snapshot() {
    base.Snapshot();
    _renderHoles = string.Concat(
      _holes.Select(h =>
        h.Molten ? 'm'
        : h.Charge > 0 ? 'c'
        : h.Occupied ? 'p'
        : '.'
      )
    );
    if (_holes.Any(h => h.Preheat > 0f))
      _renderHoles += "+";
  }

  protected override string RenderKey(int layers, string texture) =>
    $"{layers}|{texture}|{_renderHoles}";

  protected override List<string> RenderElements(int layers) =>
    [
      .. CrucibleHearthLayout.ElementsFor(
        [.. _holes.Select(Drawn)],
        Bed,
        layers
      ),
    ];

  // A lidded hole is a working one: the cover goes on once the pot starts taking heat, so an idle pot
  // stays visible and a pot in its heat is shut in.
  private static CrucibleHearthLayout.HoleContents Drawn(CrucibleHole hole) =>
    new(
      Pot: hole.Occupied,
      Charge: hole.Charge > 0,
      Slag: hole.Molten,
      Cover: hole.Occupied && hole.Preheat > 0f
    );

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    for (int i = 0; i < _holes.Length; i++) {
      CrucibleHole hole = _holes[i];
      tree.SetBool($"hole{i}pot", hole.Pot);
      tree.SetInt($"hole{i}firings", hole.Firings);
      tree.SetInt($"hole{i}charge", hole.Charge);
      tree.SetFloat($"hole{i}preheat", hole.Preheat);
      tree.SetFloat($"hole{i}melt", hole.Melt);
      tree.SetInt($"hole{i}metal", hole.Metal);
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    for (int i = 0; i < _holes.Length; i++)
      _holes[i]
        .Restore(
          tree.GetBool($"hole{i}pot"),
          tree.GetInt($"hole{i}firings"),
          tree.GetInt($"hole{i}charge"),
          tree.GetFloat($"hole{i}preheat"),
          tree.GetFloat($"hole{i}melt"),
          tree.GetInt($"hole{i}metal")
        );
    // After the holes, because the base re-snapshots the mesh from what it reads and the holes are half of
    // what that mesh draws.
    base.FromTreeAttributes(tree, worldForResolving);
  }

  #endregion

  #region Break safety

  /// <summary>
  /// Whatever the holes were holding, so a hearth broken mid-heat does not swallow four pots and their
  /// charge. Called by the block, which drops the bed's fuel the same way.
  /// </summary>
  public IEnumerable<ItemStack> HoleDrops() {
    foreach (CrucibleHole hole in _holes) {
      if (!hole.Occupied)
        continue;
      ItemStack? pot = hole.Molten
        ? CruciblePot.Molten(Api!.World, hole.Firings, hole.Metal)
        : CruciblePot.Burned(Api!.World, hole.Firings);
      if (pot != null)
        yield return pot;
      if (!hole.Molten && hole.Charge > 0)
        foreach (ItemStack stack in BlisterStacks(hole.Charge))
          yield return stack;
      hole.Clear();
    }
  }

  private IEnumerable<ItemStack> BlisterStacks(int units) {
    int chunks = units / BlisterBreaking.ChunkUnits;
    int bits =
      (units - chunks * BlisterBreaking.ChunkUnits) / BlisterBreaking.BitUnits;
    foreach (
      (string code, int count) in new[]
      {
        (BlisterBreaking.ChunkCode, chunks),
        (BlisterBreaking.BitCode, bits),
      }
    ) {
      if (count <= 0)
        continue;
      Item? item = Api?.World.GetItem(new AssetLocation(code));
      if (item != null)
        yield return new ItemStack(item, count);
    }
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    for (int i = 0; i < _holes.Length; i++) {
      CrucibleHole hole = _holes[i];
      if (!hole.Occupied)
        continue;

      dsc.AppendLine(
        hole.Molten ? Lang.Get(IiexLang.CrucibleHoleMolten, i + 1, hole.Metal)
        : !hole.Preheated
          ? Lang.Get(
            IiexLang.CrucibleHolePreheating,
            i + 1,
            (int)(hole.PreheatFraction * 100)
          )
        : !hole.Charged
          ? Lang.Get(IiexLang.CrucibleHoleShort, i + 1, hole.Charge)
        : Lang.Get(
          IiexLang.CrucibleHoleMelting,
          i + 1,
          (int)(hole.MeltFraction * 100)
        )
      );
    }
  }

  #endregion
}
