using System;
using System.Text;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkMolten;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SteelmakingExpanded.BlockStructures.Converter.BlockEntities;

/// <summary>
/// The 3×3×3 converter shell. Construction runs through the <c>RightClickConstructable</c>
/// behavior, which suppresses the default mesh, so the vessel renders through the animator: a
/// permanent <c>idle</c> animation re-tessellated to the built elements, with <c>filling</c> and
/// <c>pouring</c> as held tilt poses. Operational pose and charge are mirrored from its
/// <see cref="BlockEntityConverterControl"/>; solidified drops are handed back on break.
/// </summary>
[BlockEntityRegister]
public class BlockEntityConverterBessemer : BlockEntity, IChiselableMolten {
  private BlockPos? _controlPos;
  private ConverterOpState _opState = ConverterOpState.Normal;
  private bool _solidified;
  private int _chargeUnits;

  // Owns the RCC-suppressed-mesh animator triad (shared by every constructed mega-block).
  private ConstructedAnimator? _animator;

  /// <summary>True once the player has finished the construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  /// <summary>Whether the mirrored charge has solidified inside the vessel.</summary>
  public bool IsSolidified => _solidified;

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The animator (and IsConstructed) is resolved on both sides; it only builds/poses on the client.
    _animator = new ConstructedAnimator(this, () => AnimCacheKey);
    _animator.Initialize(ApplyPose);
  }

  private string AnimCacheKey =>
    BlockConverterBessemer.BaseCode + "-" + Block.Variant["side"];

  public override void OnBlockRemoved() {
    _animator?.Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _animator?.Dispose();
    base.OnBlockUnloaded();
  }

  #endregion

  #region Control link

  /// <summary>Records the position of the control block that drives this vessel.</summary>
  public void LinkControl(BlockPos controlPos) {
    _controlPos = controlPos.Copy();
    MarkDirty(true);
  }

  private BlockEntityConverterControl? GetControl() =>
    _controlPos == null
      ? null
      : Api.World.BlockAccessor.GetBlockEntity(_controlPos)
        as BlockEntityConverterControl;

  /// <summary>Server-side mirror update pushed by the control.</summary>
  public void UpdateMirror(
    bool solidified,
    int chargeUnits,
    ConverterOpState state
  ) {
    bool changed =
      _solidified != solidified
      || _chargeUnits != chargeUnits
      || _opState != state;
    _solidified = solidified;
    _chargeUnits = chargeUnits;
    _opState = state;
    if (changed)
      MarkDirty(true);
  }

  #endregion

  #region Animation

  // Spawn box for the rising smoke (block-relative): the opening in the InputLining element,
  // rotated per orientation by RotateXZ. Only spawns while upright, so no tilt math.
  private const float LiningX1 = -0.375f,
    LiningX2 = 0f;
  private const float LiningZ1 = 0.3125f,
    LiningZ2 = 0.6875f;
  private const float LiningY1 = 1.5f,
    LiningY2 = 2.0f;

  /// <summary>Emits rising smoke from the vessel mouth while refining; called from the control's tick.</summary>
  public void SpawnSmokeParticles(float intensity = 1f) {
    // Called from the control's server tick; server-spawned particles replicate to clients, so
    // don't gate on the client API here.
    if (Api == null)
      return;

    float x1 = LiningX1,
      z1 = LiningZ1;
    float x2 = LiningX2,
      z2 = LiningZ2;
    RotateXZ(ref x1, ref z1);
    RotateXZ(ref x2, ref z2);

    Vec3d minPos = new(
      Pos.X + Math.Min(x1, x2),
      Pos.Y + LiningY1,
      Pos.Z + Math.Min(z1, z2)
    );
    Vec3d maxPos = new(
      Pos.X + Math.Max(x1, x2),
      Pos.Y + LiningY2,
      Pos.Z + Math.Max(z1, z2)
    );

    ExParticles.RisingPlume(
      Api.World,
      ExParticles.Smoke,
      minPos,
      maxPos,
      // Low upward velocity and short life keep the plume close to the vessel mouth.
      new Vec3f(-0.15f, 0.5f, -0.15f),
      new Vec3f(0.15f, 1.1f, 0.15f),
      intensity * 6f,
      intensity * 10f,
      0.7f,
      -0.05f,
      0.15f,
      0.45f,
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, -120f),
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1f)
    );
  }

  // Rotates a block-relative (x,z) around the block centre to match the shape's rotateY.
  private void RotateXZ(ref float x, ref float z) =>
    ExOrientation.RotateAroundCenter(
      ref x,
      ref z,
      ExOrientation.AngleFromSide(Block.Variant["side"])
    );

  private void ApplyPose() =>
    _animator?.Pose(util => {
      util.StopAnimation("idle");
      util.StopAnimation("filling");
      util.StopAnimation("slagpouring");
      util.StopAnimation("pouring");

      // Pose tilts apply only once the vessel is built; during construction it renders the partial
      // mesh at rest through "idle". Slag pours off a shallow tilt, since it floats, and the steel
      // off the deeper pour tilt beneath it.
      string code = (IsConstructed ? _opState : ConverterOpState.Normal) switch {
        ConverterOpState.Filling => "filling",
        ConverterOpState.SlagPouring => "slagpouring",
        ConverterOpState.SteelPouring => "pouring",
        _ => "idle",
      };

      util.StartAnimation(
        new AnimationMetaData {
          Animation = code,
          Code = code,
          // The whole vessel tilts, so the pose runs slow. Idle only holds it visible.
          AnimationSpeed = code == "idle" ? 1f : 0.3f,
          EaseInSpeed = 3f,
          EaseOutSpeed = 3f,
        }.Init()
      );
    });

  #endregion

  #region Break handoff

  /// <summary>
  /// Returns the solidified drops to scatter (from the control's charge) and
  /// clears the control's charge. Returns null when nothing solidified.
  /// </summary>
  public ItemStack? CollectBreakDrops() => GetControl()?.OnConverterBroken();

  #endregion

  #region Chisel-out (IChiselableMolten, forwarded to the control)

  /// <summary>True when the vessel holds a solidified charge (see the control).</summary>
  public bool HasSolidifiedCharge => GetControl()?.HasSolidifiedCharge ?? false;

  /// <summary>True when the solidified charge has cooled to the chisellable (hardened) threshold.</summary>
  public bool ChargeIsHardened => GetControl()?.ChargeIsHardened ?? false;

  /// <summary>True when a small hardened residue can be chiselled out instead of breaking the vessel.</summary>
  public bool CanChiselOut() => GetControl()?.CanChiselOut() ?? false;

  /// <summary>Server-side: chips the hardened residue out via the control; null when not chiselable.</summary>
  public ItemStack? ChiselOutContent() => GetControl()?.ChiselOutContent();

  // IChiselableMolten: the solidified charge is the chiselable target. Unlike a canal it also caps by
  // size, so the blocked feedback separates "too full" (hardened but too big) from "too hot".
  bool IChiselableMolten.HasChiselableContent => HasSolidifiedCharge;
  bool IChiselableMolten.CanChiselOut => CanChiselOut();
  string? IChiselableMolten.ChiselBlockedError =>
    ChargeIsHardened ? "smex-bessemertoofull" : "smex-bessemertoohot";

  ItemStack? IChiselableMolten.ChiselOut() => ChiselOutContent();

  #endregion

  #region HUD

  /// <summary>
  /// The vessel carries the live operational readout (charge, progress, power, status). The state
  /// lives on the control block, which builds the text.
  /// </summary>
  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    // During construction the RCC interaction help covers what's next - no operational state yet.
    if (!IsConstructed)
      return;

    GetControl()?.AppendStructureState(forPlayer, dsc);
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_controlPos != null) {
      tree.SetInt("ctrlX", _controlPos.X);
      tree.SetInt("ctrlY", _controlPos.Y);
      tree.SetInt("ctrlZ", _controlPos.Z);
    }
    tree.SetInt("opState", (int)_opState);
    tree.SetBool("solidified", _solidified);
    tree.SetInt("chargeUnits", _chargeUnits);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    if (tree.HasAttribute("ctrlX"))
      _controlPos = new BlockPos(
        tree.GetInt("ctrlX"),
        tree.GetInt("ctrlY"),
        tree.GetInt("ctrlZ")
      );

    var prevState = _opState;
    _opState = (ConverterOpState)tree.GetInt("opState");
    _solidified = tree.GetBool("solidified");
    _chargeUnits = tree.GetInt("chargeUnits");

    if (Api?.Side == EnumAppSide.Client && prevState != _opState)
      ApplyPose();
  }

  #endregion
}
