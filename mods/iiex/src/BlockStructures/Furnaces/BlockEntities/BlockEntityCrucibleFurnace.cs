using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The crucible furnace (Huntsman, 1740): a deep coke fire with four sealed pots standing in it, drawn by
/// a stack tall enough to reach 1600 C on natural draught alone. A
/// <see cref="BlockEntityFireboxFurnace"/> - no blast, no tuyeres, plain fuel - whose hearth is the fuel
/// bed rather than a bed beside one.
/// </summary>
/// <remarks>
/// The damper is the whole operating rhythm, and it is thrown once, in the middle of a heat: shut to bring
/// cold pots up gently, open for the full draught that melts them. Opening it early destroys the pots;
/// leaving it shut is a fire that never reaches temperature. See
/// docs/design/machines/crucible-furnace.md.
/// </remarks>
[BlockEntityRegister]
public class BlockEntityCrucibleFurnace : BlockEntityFireboxFurnace {
  #region Geometry

  // No `ShaftMin`/`ShaftMax` override: the drawing marks its one `H` cell `FurnaceCellRoles.Firebox` and the base
  // derives the same single-cell box from it. The fire and the work are the same cell here, which is what
  // separates this furnace from the two reverberatory hearths.

  /// <summary>
  /// The hearth cell, at column 1 of row 2 against this drawing's <c>Origin(-3, -2)</c>. Both the fuel bed
  /// and the pots stand in it - a crucible furnace has one hole row and the fire is packed round it.
  /// </summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 0);

  /// <summary>
  /// The work door, in front of the hearth rather than a course above it: this furnace's mouth is at floor
  /// level, where the two reverberatory ones open a course up.
  /// </summary>
  /// <remarks>
  /// Overridden and not inherited. The branch's default is the offset both reverberatory drawings agree
  /// on, and on this drawing that cell is brick - so an inherited door would resolve to nothing and
  /// <c>Venting</c> would read false with the door standing wide open.
  /// </remarks>
  protected override Vec3i? DoorCell => new(-2, 0, 1);

  #endregion

  #region Parts

  /// <summary>
  /// The hearth the pots stand in. Resolved by the branch on the same schedule as the taps - at
  /// completion, on load and on both lit-tick recovery paths - so a hearth broken and replaced mid-heat is
  /// picked up again without a reload.
  /// </summary>
  public BlockEntityCrucibleHearth? Hearth =>
    HearthPart as BlockEntityCrucibleHearth;

  /// <summary>The chimney damper, at the foot of the stack rather than over its top.</summary>
  /// <remarks>
  /// Read off <see cref="FurnaceCellRoles.Damper"/> rather than off the branch's own cap, which it resolves at the
  /// cell above the highest flue course. On this furnace everything above that course is the player's
  /// chimney, so the branch would find brick or sky and report an unregulated stack - which reads as
  /// permanently open, and permanently open is what destroys every pot.
  /// </remarks>
  public BlockEntityPuddlingChimneyCap? Damper {
    get {
      IReadOnlyList<BlockPos> cells = CellsWithRole(FurnaceCellRoles.Damper);
      return cells.Count == 0
        ? null
        : Api?.World.BlockAccessor.GetBlockEntity(cells[0])
          as BlockEntityPuddlingChimneyCap;
    }
  }

  /// <summary>Shut by default, which is the position a heat starts in.</summary>
  protected override bool DamperOpen => Damper?.IsOpen ?? false;

  #endregion

  #region The chimney

  // Counted, not drawn. The layout fixes the flue and one base course; every course above it is the
  // player's, and how many they laid is the machine's temperature dial. Re-walked once per production tick
  // rather than per read: ComputeHeatBalance asks for this several times a tick and the block info asks
  // again on every look.
  private int _builtCourses;

  /// <summary>
  /// Flue courses over the fire: the ones the drawing declares, plus every valid course the player stacked
  /// on top of the last one.
  /// </summary>
  protected override int StackCourses => base.StackCourses + _builtCourses;

  /// <summary>The course count at which a stack pulls hardest, past which it pulls less.</summary>
  public static int PeakCourses => IiexValues.CrucibleStackCourses;

  /// <summary>
  /// The chimney this furnace is designed around: the peak of the draught curve. A player who builds it
  /// gets everything the stack has to give, and a shorter one is a colder furnace rather than a broken one.
  /// </summary>
  public override int RatedStackCourses => PeakCourses;

  /// <summary>
  /// Walks up from the highest drawn flue course, counting courses the player built. A course is one air
  /// cell ringed by brick on all four sides - the same shape siex's smokestack draws - and the walk stops
  /// at the first cell that is not one, so a gap breaks the count rather than being stepped over.
  /// </summary>
  /// <remarks>
  /// The core owns the walk and nothing searches back down for the core, which is what keeps the chimney
  /// off <c>ComponentScanBelow</c>'s eight-cell leash. Hard-capped so a player stacking sixty courses costs
  /// a bounded walk; past the peak the draught declines anyway.
  /// </remarks>
  public int WalkChimney() {
    IReadOnlyList<BlockPos> flue = CellsWithRole(FurnaceCellRoles.Flue);
    if (flue.Count == 0 || Api?.World == null)
      return 0;

    BlockPos above = flue.Aggregate((a, b) => b.Y > a.Y ? b : a).UpCopy();
    int courses = 0;
    while (courses < MaxWalkedCourses && IsCourse(above)) {
      courses++;
      above = above.UpCopy();
    }
    return courses;
  }

  private bool IsCourse(BlockPos centre) {
    IBlockAccessor blocks = Api.World.BlockAccessor;
    if (blocks.GetBlock(centre).Id != 0)
      return false;

    foreach (BlockFacing side in BlockFacing.HORIZONTALS)
      if (
        !WildcardUtil.Match(
          new AssetLocation(VanillaCodes.AnyBricks),
          blocks.GetBlock(centre.AddCopy(side)).Code
        )
      )
        return false;
    return true;
  }

  // Far past the peak, so the cap is never what a player runs into; it exists so a walk cannot run the
  // height of the world.
  private const int MaxWalkedCourses = 32;

  #endregion

  #region The heat

  /// <summary>
  /// The furnace's own tick, plus the pass the melt cycle cannot do: the pots' preheat and the crack.
  /// </summary>
  /// <remarks>
  /// It has to be here and not in <see cref="SmeltCycle"/>. The core calls the melt cycle only once the
  /// chamber is at process temperature, and a furnace with its damper shut never gets there - so the
  /// preheat, which is the phase the damper is shut for, would never run, and a pot taken into the full
  /// fire too early would never be looked at.
  /// </remarks>
  protected override void OnProductionTick(float dt) {
    _builtCourses = WalkChimney();
    base.OnProductionTick(dt);

    if (State != FurnaceState.Idle)
      Hearth?.FireTick(dt, DamperOpen);
  }

  /// <summary>
  /// One melt cycle over the four holes. Each is its own heat: a pot seated late finishes late rather than
  /// riding the others' progress.
  /// </summary>
  /// <remarks>
  /// Advances by the configured interval rather than by <paramref name="dt"/>, which is what makes a stack
  /// worth building past the melting point: the core shortens the gap between cycles as the furnace runs
  /// further above its melt line, so a hotter fire spends more cycles in the same wall-clock and the heat
  /// comes off sooner.
  /// </remarks>
  protected override void SmeltCycle(object chargeHandle, float dt) {
    Hearth?.MeltStep(IiexValues.CrucibleMeltIntervalSec);
  }

  #endregion

  #region Draught and losses

  /// <summary>
  /// Nothing. The pots stand in the fire rather than across a bridge from it, which is the case the base
  /// value is written for; the reverberatory figure the firebox branch carries is the cost of throwing a
  /// flame over a hearth, and this furnace never does.
  /// </summary>
  protected override float TransferLoss => 0f;

  /// <summary>
  /// Half a plain firebox's, because the charge is walled off from the fire rather than lying in it. Four
  /// sealed pots on their stands, not a burden the flame plays over.
  /// </summary>
  protected override float ChargeLossFull => IiexValues.CrucibleChargeLoss;

  #endregion

  #region Tunables

  // Crucible steel's own melting point, and the highest in the mod: the whole machine exists to reach it.
  // Reachable only with the two loss overrides above and a chimney near the draught curve's peak - six
  // courses is where it first clears, nine is the optimum. See docs/design/machines/crucible-furnace.md.
  protected override float MeltingPoint => IiexValues.CrucibleMeltingPointC;

  protected override float MeltIntervalSec =>
    IiexValues.CrucibleMeltIntervalSec;

  #endregion

  #region HUD

  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.CruciblefurnaceReady));

  /// <summary>
  /// The chimney and the damper: the only two things the player controls, and neither visible from outside
  /// the furnace. A declining draught past the peak is indistinguishable from a bug unless the peak is
  /// named, which is why the line carries a direction rather than only a number.
  /// </summary>
  protected override void AppendHeatExtras(StringBuilder sb) {
    base.AppendHeatExtras(sb);

    int courses = StackCourses;
    int draught = (int)(
      StackDraught.NaturalDraughtFor(courses, DamperOpen, Venting) * 100f
    );

    sb.AppendLine(
      courses < PeakCourses
        ? Lang.Get(
          IiexLang.CruciblefurnaceStackRising,
          courses,
          draught,
          PeakCourses
        )
      : courses == PeakCourses
        ? Lang.Get(IiexLang.CruciblefurnaceStackPeak, courses, draught)
      : Lang.Get(
        IiexLang.CruciblefurnaceStackPast,
        courses,
        draught,
        PeakCourses
      )
    );
    sb.AppendLine(
      Lang.Get(
        DamperOpen
          ? IiexLang.CruciblefurnaceDamperMelt
          : IiexLang.CruciblefurnaceDamperPreheat
      )
    );
    if (Hearth is null)
      sb.AppendLine(Lang.Get(IiexLang.CruciblefurnaceNohearth));
  }

  #endregion
}
