using System.Text;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The heating (reheat) furnace: a <see cref="BlockEntityFireboxFurnace"/> that <b>melts nothing</b>.
/// Stock is laid on the hearth, the firebox flame is drawn over it, and it comes back out hot enough to
/// roll. It is the first furnace in the suite with no molten pool, no tap and no product to freeze -
/// which is exactly why the fired core stopped demanding those. It overrides none of the molten-product
/// members and inherits their truthful defaults (see <see cref="BlockEntityFurnaceCore"/>).
/// <para>
/// Everything that makes it reverberatory - natural draught, no tuyeres, plain fuel - is the branch's;
/// what is left here is where its own firebox and hearth sit, and how hot it runs.
/// </para>
/// <para>
/// <b>Incomplete.</b> This is the structural shell: the multiblock stands, lights, burns its firebox and
/// holds heat. The hearth itself - three rows of typed stock, the row access rule, and the heat that
/// actually goes into the pieces - is not built, and neither is the roasting mode that will share this
/// machine. See <c>docs/design/iwex.md</c> and <c>conventions.md</c> § metal recovery.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHeatingFurnace : BlockEntityFireboxFurnace
{
  #region Reverberatory geometry

  // There is deliberately no `ShaftMin`/`ShaftMax` override here: the drawing marks both `c` cells
  // (col 1, rows 1-2 of layer 1, i.e. `(-5, 1, 0)` and `(-5, 1, 1)` after the Origin(-6,-1) shift) as
  // `CellRole.Firebox`, and the base derives the same two-cell box from it. Two fuel cells along one
  // side, not a column under the work.

  /// <summary>The hearth cell - where the work sits, deliberately out of the fire.</summary>
  protected override Vec3i ShaftCentre => new(-2, 0, 1);

  #endregion

  #region Firebox charge

  /// <summary>
  /// The "melt" cycle for a furnace that melts nothing. Soaking heat into the stock on the hearth belongs
  /// here once the hearth rows exist; until then a lit heating furnace simply holds its temperature, which
  /// is already the useful half.
  /// <para>
  /// Reading and igniting the firebox is the branch's (<see cref="BlockEntityFireboxFurnace"/>); the cycle
  /// stays per-machine, because what a hearth does once it is hot is the one thing the two hearths will
  /// never share.
  /// </para>
  /// </summary>
  protected override void SmeltCycle(object chargeHandle, float dt) { }

  #endregion

  #region Tunables

  // Reheat temperature, not a melting point: the furnace's job is to get wrought stock back above the
  // rolling floor, well under the 1482 C where it would start to melt.
  protected override float MeltingPoint => IwexValues.RollingTempC;

  #endregion

  #region HUD

  protected override void AppendReadyInfo(StringBuilder sb) =>
    sb.AppendLine(Lang.Get("iwex:heatingfurnace-ready"));

  #endregion
}
