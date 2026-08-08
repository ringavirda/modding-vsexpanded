using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cold blast furnace. Its behaviour lives in <see cref="BlockEntityShaftFurnace"/>; the blast
/// arrives at ambient off a mechanical blower, so the heat balance grants no preheat and the burden
/// carries the coke instead. See docs/design/machines/blast-furnace-cold.md.
/// <para>
/// The body is empty on purpose: the open top is the chimney and the layout places plain refractory
/// brick where the hot furnace carries its pipe outlets, so no gas outlets are derived. Overriding
/// <c>GasOutletCells</c> here would re-run the outlet scan against brick every lit tick and leave
/// <see cref="BlockEntityFurnaceCore.IsChoked"/> unreachable.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnaceCold : BlockEntityShaftFurnace { }
