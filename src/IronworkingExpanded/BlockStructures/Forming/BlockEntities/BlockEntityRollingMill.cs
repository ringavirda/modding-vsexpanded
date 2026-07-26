using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Forming.BlockEntities;

/// <summary>
/// Block entity for the rolling mill: the first <b>consumer</b> of the mechanical-energy network
/// (<c>docs/design/mp-energy-network.md</c>). It sits on the <c>mpenergy</c> run its axle line forms and, once
/// the pass simulation lands (Phase C), imposes a resisting <see cref="LoadTorque"/> while a pass is under the
/// rolls; the pass advances only while the shaft keeps turning, and jams if the load drags ω to a stall (the
/// "keep a flywheel on the line, keep the stock hot" coupling).
/// <para>
/// Phase A scope: the mill is an <see cref="IMpEnergyConsumer"/> that is registered on the run but idle -
/// it loads nothing until a roll set is fitted and stock is worked. The graph membership is handled by the
/// <see cref="BlockEntityNetworkNode"/> base (auto <c>AddNode</c>/<c>RemoveNode</c>); the pass state machine,
/// the torque curve, and the feed-side-by-rotation logic are follow-ups.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityRollingMill : BlockEntityNetworkNode, IMpEnergyConsumer
{
  public override string NetworkType
  {
    get => "mpenergy";
    set { }
  }

  /// <summary>The resisting torque the mill imposes on its run at shaft speed <paramref name="speed"/>. Idle in
  /// Phase A - a pass (Phase C) sets it from the stock temperature and the gap reduction (cold or thick stock
  /// loads harder, dragging ω down), dropping back to 0 once the piece clears the rolls.</summary>
  public float LoadTorque(float speed) => 0f;
}
