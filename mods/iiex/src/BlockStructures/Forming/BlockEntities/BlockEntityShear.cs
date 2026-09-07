using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Catalogues;
using ExpandedLib.Machines;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronIndustryExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for the crop shear, a consumer on the mechanical-energy network. A stroke is one bite:
/// stock at a named stage goes in, one product leaves and the remainder stays stock at the same stage with
/// one more crop tallied against it. Every crop in the forming route passes through this block, which is
/// what lets the mill be a pure reduction machine.
/// <para>
/// Cold stock is not refused - shearing needs force rather than friction and cold metal parts more cleanly
/// - it simply costs more drive. The decision itself lives in <see cref="ShearFeed"/>, which is pure; this
/// class owns the tooling slot, the stroke clock and the network reads.
/// See docs/design/machines/shear.md.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityShear : BlockEntityMpBench {
  #region Inventory

  /// <summary>The fitted blade set - the tooling whose temper gates which jobs the shear will take.</summary>
  public const int BladeSlot = 0;

  /// <summary>The piece under the blades, held only for the duration of a stroke.</summary>
  public const int PieceSlot = 1;

  public override string InventoryClassName => "shear";

  protected override MachineSlotSpec[] SlotSpecs =>
    [
      MachineSlotSpec.Input(MachineTool.IsTool, "#6A5A3A"),
      MachineSlotSpec.AnyInput(),
    ];

  private ItemStack? _blades {
    get => Inventory[BladeSlot].Itemstack;
    set => Inventory[BladeSlot].Itemstack = value;
  }

  private ItemStack? _piece {
    get => Inventory[PieceSlot].Itemstack;
    set => Inventory[PieceSlot].Itemstack = value;
  }

  #endregion

  #region The stroke

  // Heat of the piece under the blades, which is what the cold multiplier is applied to. The job is
  // re-resolved on completion rather than stored, so retuning a crop table reaches a piece already under
  // the blades.
  [Persist("shearTempC")]
  private float _tempC;

  /// <summary>The temper of the fitted blades, or <c>-1</c> when the nest is bare.</summary>
  public int BladeTier => MachineTool.TierOf(_blades);

  /// <summary>Whether a blade set is fitted.</summary>
  public bool HasBlades => BladeTier >= 0;

  /// <summary>The catalogue the shear reads its crops from - the process-wide one, which every mod's job
  /// tables load into. Settable so a fixture can stand a crop up without writing to the shared
  /// catalogue.</summary>
  public ProcessJobRegistry Jobs { get; protected set; } =
    ProcessJobRegistry.Shared;

  /// <summary>The machine key the shear's jobs are declared under.</summary>
  public const string MachineKey = "shear";

  /// <summary>
  /// The crop declared for <paramref name="stack"/> at its current gauge, or null when nothing is. A stage
  /// that names no job is not a stopping point, which is how the route stays open for a mod to close.
  /// </summary>
  public ProcessJob? JobFor(ItemStack? stack) {
    if (stack?.Collectible?.Code is not { } code)
      return null;
    // Family is the roller branch the piece came down, not its form: a fork in the route is what a job
    // disambiguates, and the form is already carried by the input code.
    WorkPiece? piece = WorkPiece.FromStack(stack);
    return Jobs.Job(
      MachineKey,
      code.ToShortString(),
      piece?.Thickness,
      piece?.Family
    );
  }

  /// <summary>
  /// Offers <paramref name="stack"/> to the blades. On acceptance the piece goes under them and the
  /// product is handed back when the stroke clears; the verdict is returned on refusal too, so the caller
  /// can report which mistake was made.
  /// </summary>
  public ShearDecision TryCrop(ItemStack? stack) {
    if (IsStroking)
      return new ShearDecision(ShearVerdict.NotTurning, null, 0f);

    float tempC =
      stack != null && Api != null
        ? stack.Collectible.GetTemperature(Api.World, stack)
        : 0f;

    ShearDecision decision = ShearFeed.Decide(
      HasBlades,
      BladeTier,
      JobFor(stack),
      WorkPiece.FromStack(stack),
      tempC,
      IiexValues.RollingTempC,
      IiexValues.ShearColdMultiplier,
      AvailableTorque,
      Speed
    );
    if (!decision.Accepted || stack == null)
      return decision;

    // The crop is not tallied yet: it is committed in CompleteStroke, so an interrupted stroke leaves the
    // stock exactly as it went in - the same rule the mill's pass follows.
    _piece = stack.Clone();
    _piece.StackSize = 1;
    _tempC = tempC;
    BeginStroke(decision.Job!.Seconds);
    MarkDirty(true);
    return decision;
  }

  /// <summary>The cut itself: the crop is committed here and nowhere else, so an interrupted stroke leaves
  /// the stock exactly as it went in.</summary>
  protected override void CompleteStroke() {
    ItemStack? input = _piece;
    _piece = null;

    // Re-resolved rather than carried from TryCrop: a table retuned while the piece was under the blades
    // should reach it, and a piece whose job has gone comes back untouched rather than vanishing.
    ProcessJob? job = JobFor(input);
    if (input == null || job == null || Api == null) {
      Eject(input);
      MarkDirty(true);
      return;
    }

    // A staged job hands one product per stroke; a whole-item job hands the lot and keeps nothing back.
    Eject(Resolve(job.Output, job.Stage == null ? job.Count : 1));
    Eject(Remainder(input, job));
    MarkDirty(true);
  }

  /// <summary>
  /// What is left of the input after one crop. A staged job leaves the piece as stock at the same stage
  /// with one more crop tallied; a whole-item job consumes it, so nothing comes back.
  /// </summary>
  private static ItemStack? Remainder(ItemStack input, ProcessJob job) {
    if (job.Stage == null)
      return null;
    if (WorkPiece.FromStack(input) is not { } piece)
      return input;

    piece.Crop(job.Count).ToStack(input);
    return input;
  }

  /// <summary>
  /// Takes a piece back out from under the blades, backing the wrench interaction. The stack is returned
  /// unchanged, since the interrupted crop was never tallied. Returns null when nothing is stuck.
  /// </summary>
  public ItemStack? ReleaseStuckPiece() {
    if (!IsStroking)
      return null;
    ItemStack? piece = _piece;
    _piece = null;
    AbandonStroke();
    MarkDirty(true);
    return piece;
  }

  #endregion

  #region Tooling

  /// <summary>
  /// Fits <paramref name="blades"/> to the nest and hands back through <paramref name="previous"/>
  /// whatever was there. Refused mid-stroke; the tooling cannot change with a piece under it.
  /// </summary>
  public bool TryFitBlades(ItemStack? blades, out ItemStack? previous) {
    previous = null;
    if (IsStroking)
      return false;
    if (blades != null && !MachineTool.IsTool(blades))
      return false;

    previous = _blades;
    _blades = blades;
    MarkDirty(true);
    return true;
  }

  #endregion

  #region Network

  /// <summary>
  /// What the shear draws off the run: the stroke's own demand while a piece is under the blades and
  /// nothing when the nest is empty. An idle shear is a free passthrough on the line shaft, the same
  /// contract the mill's stand keeps.
  /// </summary>
  public override float LoadTorque(float speed) =>
    IsStroking && JobFor(_piece) is { } job
      ? ShearFeed.RequiredTorque(
        job,
        _tempC,
        IiexValues.RollingTempC,
        IiexValues.ShearColdMultiplier
      )
      : 0f;

  #endregion

  #region Block info

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    dsc.AppendLine(
      HasBlades
        ? Lang.Get("iiex:shear-blades-fitted", BladeTier)
        : Lang.Get("iiex:shear-blades-none")
    );
    if (IsStroking)
      dsc.AppendLine(
        Lang.Get("iiex:shear-stroking", Remaining.ToString("0.0"))
      );
  }

  #endregion

  public override void OnBlockBroken(IPlayer? byPlayer = null) {
    // The blades and any piece mid-stroke come back rather than being destroyed with the machine.
    Eject(_blades);
    Eject(_piece);
    _blades = null;
    _piece = null;
    base.OnBlockBroken(byPlayer);
  }
}
