using System.Text;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// Reverberatory puddling furnace. Inherits the fire, heat-balance and extinguish path of
/// <see cref="BlockEntityFireboxFurnace"/> unchanged; the charge box the base walks is the firebox cell
/// beside the hearth, so fuel burns clear of the work. Puddled iron leaves as pasty balls through the
/// door, so the furnace has no molten pool and no tap and inherits the defaults of
/// <see cref="BlockEntityFurnaceCore"/>'s molten-product members.
/// <para>
/// Incomplete: the puddling cycle and the tap-cinder yield on cleaning are not built. The fire, the
/// draught and the process temperature are. See docs/design/machines/puddling-furnace.md.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingFurnace : BlockEntityFireboxFurnace {
  #region Reverberatory geometry

  // No shaft bounds are declared here: the base derives the one-cell firebox from the layout cell marked
  // CellRole.Firebox.

  /// <summary>The hearth's own cell, where the work sits, away from the fire.</summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 0);

  // No tap cells either. The layout marks neither the MetalTap nor the SlagTap role, so MetalTapPos and
  // SlagTapPos are null on this furnace: a reverberatory hearth is raked out, not tapped.

  #endregion

  #region Tunables

  // A process temperature, not a melting point: this furnace never melts iron, and the whole reason it
  // works is that 1400 sits between pig's 1200 and iron's 1482. Reachable only with the chimney pulling
  // and the doors shut, which is what makes the damper the machine's one operating input.
  protected override float MeltingPoint => IiexValues.PuddlingProcessTempC;

  #endregion

  #region Parts

  /// <summary>
  /// The fettled bed the pigs stand on. Resolved by the branch on the same schedule as the taps - at
  /// completion, on load and on both lit-tick recovery paths - so a bed broken and replaced mid-heat is
  /// picked up again without a reload.
  /// </summary>
  public BlockEntityPuddlingHearth? Hearth =>
    HearthPart as BlockEntityPuddlingHearth;

  /// <summary>
  /// Whether the small working door stands open. Rabbling and drawing out are gated on it - the bath is
  /// worked through the small door precisely so the main door can stay shut and the heat stay in.
  /// </summary>
  public bool SmallDoorOpen => Door?.SmallOpen == true;

  /// <summary>Plays one working stroke on the door: the rabble gathering, or the paddle drawing out.</summary>
  public void PlayWorkingStroke(bool drawingOut) =>
    Door?.PlayStroke(drawingOut);

  /// <summary>
  /// How hot the bath is, which is the hearth's own temperature - a ball drawn out leaves at it and cools
  /// in the hand from there. Reading the live figure rather than a constant is what keeps a ball off a
  /// barely-holding furnace cooler than one off a furnace running properly.
  /// </summary>
  public float BathTemperature => InternalTemperature;

  #endregion

  #region Firebox charge

  /// <summary>
  /// The fire is out, so the bath sets where it lies. Nothing more can be gathered from it and the bed has
  /// to be raked and started over - a puddling furnace has no pool to freeze and drain like a shaft
  /// furnace, only a bed of metal going solid.
  /// </summary>
  protected override void ExtinguishResidue() {
    Hearth?.FreezeBath();
    base.ExtinguishResidue();
  }

  /// <summary>
  /// The puddling cycle's melt-down phase, called by the core on its melt cadence. Everything the heat
  /// does happens here rather than on a listener of the hearth's own, so it rides the core's bounded
  /// away-catch-up: a hearth ticking itself would look identical while the chunk was loaded and teleport
  /// the melt the moment it was not.
  /// </summary>
  /// <remarks>
  /// Null-tolerant throughout. A bed broken out mid-heat leaves the furnace burning with nothing to work,
  /// which reads as "no hearth" in the block info and does nothing here.
  /// </remarks>
  protected override void SmeltCycle(object chargeHandle, float dt) {
    Hearth?.MeltDown(IiexValues.PuddlingMeltFractionPerCycle);
  }

  #endregion

  #region HUD

  // One firebox cell means one pile, so there is no partially-lit state to report: the shaft branch's
  // two-branch line needs at least two piles in disagreement.
  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get(IiexLang.BfInfoReady));

  /// <summary>
  /// What the bed is doing, which the hearth's own block info cannot say: it does not know whether the
  /// furnace is lit or how far through a melt it is.
  /// </summary>
  protected override void AppendHeatExtras(StringBuilder sb) {
    base.AppendHeatExtras(sb);
    if (Hearth is not { } bed)
      sb.AppendLine(Lang.Get(IiexLang.PuddlingNohearth));
    else if (bed.HasBath)
      sb.AppendLine(
        Lang.Get(IiexLang.PuddlingBalls, bed.BallsOnBed, bed.BallsRemaining)
      );
    else if (bed.MeltProgress > 0f)
      sb.AppendLine(
        Lang.Get(
          IiexLang.PuddlingMeltingdown,
          (int)System.Math.Round(bed.MeltProgress * 100f)
        )
      );
  }

  #endregion
}
