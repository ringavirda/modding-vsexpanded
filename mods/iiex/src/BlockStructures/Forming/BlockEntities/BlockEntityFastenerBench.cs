using System.Text;
using ExpandedLib.Catalogues;
using ExpandedLib.Machines;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronIndustryExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for the two fastener benches - the nail cutter and the riveter - which are one machine with
/// two dies, two shapes and two footprints. A stroke converts a blank whole: the plate or the rod goes in
/// and every bundle it is worth comes out at once, with no remainder, which is what makes these terminal
/// stations rather than another rung of the forming ladder.
/// <para>
/// The bench reads what to do off the fitted die rather than off a table under our domain, so a
/// third party adds a bench job by shipping a die. The die also names which machine it is for, so a nail
/// die in a riveter is refused rather than quietly working.
/// See docs/design/mechanics/machining-line.md and docs/design/items/fasteners.md.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityFastenerBench : BlockEntityMpBench {
  #region Inventory

  /// <summary>The fitted die - the tooling that names the job this bench does.</summary>
  public const int DieSlot = 0;

  /// <summary>The blank under the press, held only for the duration of a stroke.</summary>
  public const int BlankSlot = 1;

  public override string InventoryClassName => "fastenerbench";

  protected override MachineSlotSpec[] SlotSpecs =>
    [
      MachineSlotSpec.Input(ItemDie.IsDie, "#6A5A3A"),
      MachineSlotSpec.AnyInput(),
    ];

  private ItemStack? _die {
    get => Inventory[DieSlot].Itemstack;
    set => Inventory[DieSlot].Itemstack = value;
  }

  private ItemStack? _blank {
    get => Inventory[BlankSlot].Itemstack;
    set => Inventory[BlankSlot].Itemstack = value;
  }

  /// <summary>Whether a die is fitted.</summary>
  public bool HasDie => ItemDie.IsDie(_die);

  #endregion

  #region The job

  /// <summary>
  /// Which bench this is, read off the block's own <c>type</c> variant rather than written down. That one
  /// read is what lets the nail cutter and the riveter be the same class: a die declares the machine its
  /// job belongs to, and this is what it is matched against.
  /// </summary>
  public string MachineKey => Block?.Variant?["type"] ?? "";

  /// <summary>
  /// The job the fitted die has for <paramref name="stack"/>, or null when it has none - no die, a die for
  /// another machine, or one that does not take this blank.
  /// </summary>
  public ProcessJob? JobFor(ItemStack? stack) {
    if (
      stack?.Collectible?.Code is not { } code
      || !ItemDie.TryParse(
        _die?.Collectible?.Attributes?[ItemDie.AttributeKey],
        out ProcessJobSet? set,
        out _
      )
    )
      return null;

    // A die names its machine, so fitting a nail die to a riveter gives the press nothing to do. Checked
    // here rather than inside ItemDie, which has no idea which bench is asking.
    if (
      !string.Equals(
        set!.Machine,
        MachineKey,
        System.StringComparison.OrdinalIgnoreCase
      )
    )
      return null;

    foreach (ProcessJob job in set.Jobs)
      if (job.Matches(code.ToShortString(), null, null))
        return job;
    return null;
  }

  /// <summary>
  /// Offers <paramref name="stack"/> to the press. On acceptance the blank goes under the die and the
  /// bundles are handed back when the stroke clears; the verdict is returned on refusal too, so the caller
  /// can report which mistake was made.
  /// </summary>
  public BenchDecision TryPress(ItemStack? stack) {
    if (IsStroking)
      return new BenchDecision(BenchVerdict.NotTurning, null);

    BenchDecision decision = BenchFeed.Decide(
      HasDie,
      JobFor(stack),
      AvailableTorque,
      Speed
    );
    if (!decision.Accepted || stack == null)
      return decision;

    _blank = stack.Clone();
    _blank.StackSize = 1;
    BeginStroke(decision.Job!.Seconds);
    MarkDirty(true);
    return decision;
  }

  /// <summary>
  /// The press coming down. The job is re-resolved rather than carried from <see cref="TryPress"/>, so a
  /// die swapped while the blank was under the press reaches it - and a blank whose job has gone comes back
  /// untouched rather than vanishing.
  /// </summary>
  protected override void CompleteStroke() {
    ItemStack? input = _blank;
    _blank = null;

    ProcessJob? job = JobFor(input);
    if (input == null || job == null || Api == null) {
      Eject(input);
      MarkDirty(true);
      return;
    }

    // A whole-item job: the blank is consumed and every bundle leaves at once. Nothing comes back, which
    // is the difference between a conversion and a crop.
    Eject(Resolve(job.Output, job.Count));
    MarkDirty(true);
  }

  /// <summary>
  /// Takes a blank back out from under the press, backing the wrench interaction. The stack is returned
  /// unchanged, since an interrupted stroke converted nothing. Returns null when nothing is stuck.
  /// </summary>
  public ItemStack? ReleaseStuckBlank() {
    if (!IsStroking)
      return null;
    ItemStack? blank = _blank;
    _blank = null;
    AbandonStroke();
    MarkDirty(true);
    return blank;
  }

  #endregion

  #region Tooling

  /// <summary>
  /// Fits <paramref name="die"/> and hands back through <paramref name="previous"/> whatever was there.
  /// Refused mid-stroke; the tooling cannot change with a blank under it.
  /// </summary>
  public bool TryFitDie(ItemStack? die, out ItemStack? previous) {
    previous = null;
    if (IsStroking)
      return false;
    if (die != null && !ItemDie.IsDie(die))
      return false;

    previous = _die;
    _die = die;
    MarkDirty(true);
    return true;
  }

  #endregion

  #region Network

  /// <summary>What the bench draws off the run: the job's own demand while a blank is under the press and
  /// nothing when it is empty. An idle bench is a free passthrough on the line shaft, the same contract
  /// the mill's stand and the shear both keep.</summary>
  public override float LoadTorque(float speed) =>
    IsStroking && JobFor(_blank) is { } job ? job.MinTorque : 0f;

  #endregion

  #region Block info

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    dsc.AppendLine(
      HasDie
        ? Lang.Get(IiexLang.BenchDieFitted, _die!.GetName())
        : Lang.Get(IiexLang.BenchDieNone)
    );
    if (IsStroking)
      dsc.AppendLine(
        Lang.Get(IiexLang.BenchStroking, Remaining.ToString("0.0"))
      );
  }

  #endregion

  public override void OnBlockBroken(IPlayer? byPlayer = null) {
    // The die and any blank mid-stroke come back rather than being destroyed with the machine.
    Eject(_die);
    Eject(_blank);
    _die = null;
    _blank = null;
    base.OnBlockBroken(byPlayer);
  }
}
