using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The beehive coke oven: two sealed chambers that turn bituminous coal into vanilla <c>game:coke</c> in
/// bulk. A <see cref="BlockEntityFireboxFurnace"/> rather than a shaft, because the branch already seals
/// the three invariants a retort also wants - no layered charge, no burden family to recognise, and an
/// ignition threshold derived from the chambers' own size rather than a hand-picked constant.
/// <para>
/// It rides vanilla's coking rather than the heat balance: a chamber bakes for
/// <c>CokeOvenCycleSec</c> and converts, on a timer and never on a temperature. See
/// <c>docs/design/machines/coke-oven.md</c>.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityCokeOven : BlockEntityFireboxFurnace {
  #region Geometry

  /// <summary>
  /// The middle of the shared wall, which is the core's own cell. A coke oven has no hearth and no bed
  /// standing clear of a fire, so this resolves nothing and is declared only because the branch reads it:
  /// the anchor is a core rather than a part, so <c>HearthPart</c> answers null and every consumer of it
  /// already tolerates that.
  /// </summary>
  protected override Vec3i ShaftCentre => new(0, 0, 0);

  /// <summary>
  /// The east chamber's drawing door, at column 6 / row 3 of layer 1 against the drawing's
  /// <c>Origin(-4, -2)</c>. The branch resolves one door and reads its open state as venting; the oven has
  /// two, and the second is the sealed gate's business rather than the branch's.
  /// </summary>
  protected override Vec3i? DoorCell => new(2, 1, 1);

  /// <summary>
  /// The chambers, as the connected groups of this oven's <c>CellRole.Firebox</c> cells - one per chamber
  /// on the shipped drawing, because the shared wall between them carries no marked cell. Derived rather
  /// than declared, so an oven of three chambers or of one would need no code.
  /// <para>
  /// Ordered by their lowest cell so a chamber keeps its index across a reload, which is what lets the
  /// bake clocks below be stored as a plain array.
  /// </para>
  /// </summary>
  public IReadOnlyList<IReadOnlyList<BlockPos>> Chambers {
    get {
      var remaining = new HashSet<BlockPos>(FireboxCells);
      var chambers = new List<IReadOnlyList<BlockPos>>();

      while (remaining.Count > 0) {
        BlockPos seed = remaining.First();
        remaining.Remove(seed);
        var group = new List<BlockPos> { seed };

        // Flood fill on face adjacency. A diagonal step would bridge two chambers that meet at a corner,
        // which no shipped drawing does but a modded one might.
        for (int i = 0; i < group.Count; i++)
          foreach (BlockFacing facing in BlockFacing.ALLFACES) {
            BlockPos next = group[i].AddCopy(facing);
            if (remaining.Remove(next))
              group.Add(next);
          }

        group.Sort(Compare);
        chambers.Add(group);
      }

      chambers.Sort((a, b) => Compare(a[0], b[0]));
      return chambers;
    }
  }

  private static int Compare(BlockPos a, BlockPos b) =>
    a.X != b.X ? a.X.CompareTo(b.X)
    : a.Y != b.Y ? a.Y.CompareTo(b.Y)
    : a.Z.CompareTo(b.Z);

  #endregion

  #region Closures

  /// <summary>
  /// The crown lids, in structure-local coordinates: column 2 and column 6 of layer 3's row 1, against the
  /// drawing's <c>Origin(-4, -2)</c>. Hand-declared like <see cref="ShaftCentre"/>, since no role marks a
  /// lid.
  /// </summary>
  protected virtual IReadOnlyList<Vec3i> LidCells =>
    [new(-2, 3, -1), new(2, 3, -1)];

  /// <summary>The drawing doors, at column 2 and column 6 of layer 1's row 3.</summary>
  protected virtual IReadOnlyList<Vec3i> DoorCells =>
    [new(-2, 1, 1), new(2, 1, 1)];

  /// <summary>
  /// The lid and drawing door belonging to <paramref name="chamber"/> - the nearest declared one of each,
  /// so the pairing survives rotation without a second table per facing and an oven of three chambers
  /// would need no code.
  /// </summary>
  /// <remarks>
  /// The core finds them, never the reverse. A lid stands three courses above the anchor and
  /// <see cref="BlockEntityFurnaceCore.ComponentScanAbove"/> reaches one, so a lid asked to resolve its own
  /// core finds nothing - which is exactly the trap the reheat furnace's hearth link fell into.
  /// </remarks>
  public (BlockEntityChargeDoor? Lid, BlockEntityChargeDoor? Door) ClosuresOf(
    IReadOnlyList<BlockPos> chamber
  ) => (Nearest(chamber, LidCells), Nearest(chamber, DoorCells));

  private BlockEntityChargeDoor? Nearest(
    IReadOnlyList<BlockPos> chamber,
    IReadOnlyList<Vec3i> candidates
  ) {
    BlockPos? best = null;
    int bestDistance = int.MaxValue;
    foreach (Vec3i local in candidates) {
      BlockPos world = GlobalOf(local);
      int distance = chamber.Min(cell =>
        Math.Abs(cell.X - world.X)
        + Math.Abs(cell.Y - world.Y)
        + Math.Abs(cell.Z - world.Z)
      );
      if (distance >= bestDistance)
        continue;
      bestDistance = distance;
      best = world;
    }
    return best == null
      ? null
      : Api?.World.BlockAccessor.GetBlockEntity(best) as BlockEntityChargeDoor;
  }

  /// <summary>
  /// Whether <paramref name="chamber"/> is shut on both openings. Coking is destructive distillation, so a
  /// chamber open to the air bakes nothing; a missing lid or door reads as open, since a hole in the crown
  /// seals no better than an open one.
  /// </summary>
  public bool Sealed(IReadOnlyList<BlockPos> chamber) {
    (BlockEntityChargeDoor? lid, BlockEntityChargeDoor? door) = ClosuresOf(
      chamber
    );
    return lid is { IsVenting: false } && door is { IsVenting: false };
  }

  #endregion

  #region Coking cycle

  /// <summary>Seconds each chamber has baked, by its index in <see cref="Chambers"/>.</summary>
  private float[] _baked = [];

  /// <summary>What the oven makes. Vanilla's own item: the mod adds no coke of its own.</summary>
  private const string CokeCode = "game:coke";

  /// <summary>Seconds <paramref name="chamber"/> has baked, for the readout.</summary>
  public float BakedSeconds(int chamber) =>
    chamber >= 0 && chamber < _baked.Length ? _baked[chamber] : 0f;

  /// <summary>
  /// One tick of the bake, per chamber. Each chamber holding raw coal accrues <paramref name="dt"/> and
  /// converts on its own clock, so a shop that charges one side and draws it while the other is still
  /// baking works exactly as a shop that runs both together.
  /// </summary>
  /// <remarks>
  /// A chamber with nothing to bake is reset rather than left standing, so the clock cannot be banked
  /// across charges: charging, drawing and recharging would otherwise hand the next load the time the last
  /// one spent. <paramref name="dt"/> is the interval this call stands for, which is what makes the bake
  /// integrate the same whether it ran on the clock or is being caught up after a reload.
  /// </remarks>
  protected override void SmeltCycle(object chargeHandle, float dt) {
    if (Api is not { Side: EnumAppSide.Server } api || dt <= 0f)
      return;

    IReadOnlyList<IReadOnlyList<BlockPos>> chambers = Chambers;
    if (_baked.Length != chambers.Count)
      Array.Resize(ref _baked, chambers.Count);

    for (int i = 0; i < chambers.Count; i++) {
      if (BedsOfChamber(chambers[i]).All(bed => !IsCokingCoal(bed.FuelCode))) {
        _baked[i] = 0f;
        continue;
      }

      // The seal gate. An open chamber holds its clock rather than losing it: opening the crown to look
      // is a mistake a player should be able to correct, and the harsher reading - a ruined bake - is not
      // what "coking only while shut" was ruled to mean.
      if (!Sealed(chambers[i]))
        continue;

      _baked[i] += dt;
      if (_baked[i] < IiexValues.CokeOvenCycleSec)
        continue;

      _baked[i] = 0f;
      Coke(api, chambers[i]);
    }
  }

  /// <summary>
  /// Turns one chamber's coal into coke. Every bed converts together - a chamber bakes as one - and a bed
  /// holding anything else is passed over, so a part-charged chamber cokes what it has rather than
  /// refusing.
  /// </summary>
  private void Coke(ICoreAPI api, IReadOnlyList<BlockPos> chamber) {
    Item? coke = api.World.GetItem(new AssetLocation(CokeCode));
    if (coke == null) {
      api.Logger.Warning(
        "[iiex] Coke oven: \"{0}\" resolves to no item, so a finished bake has nothing to become.",
        CokeCode
      );
      return;
    }

    foreach (BEBehaviorFirebox bed in BedsOfChamber(chamber)) {
      if (!IsCokingCoal(bed.FuelCode))
        continue;

      // Truncated, exactly as vanilla truncates its own conversion, so a bake can never mint fuel. A
      // chamber charged too thin to yield a whole unit loses that cell, which is what a real oven does.
      int yielded = (int)(bed.Units * IiexValues.CokeOvenYieldFrac);
      bed.Clear();
      if (yielded > 0)
        bed.TryAdd(new ItemStack(coke, yielded), yielded);
    }
    MarkDirty(true);
  }

  private IEnumerable<BEBehaviorFirebox> BedsOfChamber(
    IReadOnlyList<BlockPos> chamber
  ) =>
    chamber
      .Select(cell =>
        Api?.World.BlockAccessor.GetBlockEntity(cell)
          ?.GetBehavior<BEBehaviorFirebox>()
      )
      .Where(bed => bed != null)
      .Select(bed => bed!);

  /// <summary>Whether <paramref name="code"/> names a coal this oven bakes, by the config's fragments.
  /// Null - an empty bed - is not coal.</summary>
  private static bool IsCokingCoal(string? code) =>
    code != null
    && IiexValues.CokeOvenCokingCoals.Any(fragment =>
      code.Contains(fragment, StringComparison.OrdinalIgnoreCase)
    );

  #endregion

  #region Charge

  /// <summary>
  /// Only the coals that coke. The firebox's own test would take coke, charcoal and anthracite, none of
  /// which a retort has anything to do with - a chamber of them would bake for an hour and change nothing,
  /// which reads as a broken machine rather than as a refusal.
  /// </summary>
  public override bool AcceptsFireboxFuel(ItemStack? stack) =>
    IsCokingCoal(stack?.Collectible?.Code?.ToString());

  /// <summary>
  /// A sealed chamber does not burn its charge out. The branch's version keeps only a fraction of the bed
  /// as salvage, which is right where the bed is fuel and wrong here, where it is the work: an oven that
  /// goes out mid-bake has to leave the coal exactly where it was for the player to light again.
  /// </summary>
  protected override void BurnOutCharge() { }

  #endregion

  #region Tunables

  /// <summary>
  /// The temperature the chamber has to reach before the core will call <see cref="SmeltCycle"/> at all.
  /// Ignition heat rather than a process temperature: there is no coking temperature by ruling, so this is
  /// a formality the sealed chamber clears rather than a gate the oven can strand short of. A stated
  /// coking temperature would have needed a near-zero transfer-loss override alongside it, or the oven
  /// stalls at what natural draught alone can reach.
  /// </summary>
  protected override float MeltingPoint => IiexValues.CokeOvenLightC;

  #endregion

  #region Persistence

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("cokeChambers", _baked.Length);
    for (int i = 0; i < _baked.Length; i++)
      tree.SetFloat("cokeBaked" + i, _baked[i]);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolve
  ) {
    base.FromTreeAttributes(tree, worldForResolve);
    _baked = new float[Math.Max(0, tree.GetInt("cokeChambers"))];
    for (int i = 0; i < _baked.Length; i++)
      _baked[i] = tree.GetFloat("cokeBaked" + i);
  }

  #endregion

  #region HUD

  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.CokeovenReady));

  /// <summary>
  /// A line per chamber: what is in it, how far through the bake it is, and - the one that matters - which
  /// chamber is standing open. An oven that silently refuses to coke is indistinguishable from a broken
  /// one, which is what R7 is for.
  /// </summary>
  protected override void AppendHeatExtras(StringBuilder sb) {
    base.AppendHeatExtras(sb);

    IReadOnlyList<IReadOnlyList<BlockPos>> chambers = Chambers;
    for (int i = 0; i < chambers.Count; i++) {
      int number = i + 1;
      if (BedsOfChamber(chambers[i]).All(bed => !IsCokingCoal(bed.FuelCode))) {
        sb.AppendLine(Lang.Get(IiexLang.CokeovenChamberIdle, number));
        continue;
      }
      if (!Sealed(chambers[i])) {
        sb.AppendLine(Lang.Get(IiexLang.CokeovenChamberOpen, number));
        continue;
      }
      sb.AppendLine(
        Lang.Get(
          IiexLang.CokeovenChamberBaking,
          number,
          (int)(
            100f
            * GameMath.Clamp(
              BakedSeconds(i) / Math.Max(1f, IiexValues.CokeOvenCycleSec),
              0f,
              1f
            )
          )
        )
      );
    }
  }

  #endregion
}
