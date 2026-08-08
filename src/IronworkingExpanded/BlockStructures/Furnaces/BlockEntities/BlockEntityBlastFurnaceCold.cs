using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cold blast furnace. Everything it does lives in <see cref="BlockEntityShaftFurnace"/>; what
/// makes it the cold one is that its blast arrives at ambient off a mechanical blower, so the heat
/// balance gives it no preheat and its burden has to carry the coke instead.
/// <para>
/// The empty body is deliberate: the cold furnace's open top is its chimney (docs/design/iwex.md) and
/// its layout puts plain refractory brick where the hot furnace carries its pipe outlets, so the
/// drawing carries no outlet glyph and the furnace derives no gas outlets from it. Overriding
/// <c>GasOutletCells</c> here would make every lit tick re-run the outlet scan against brick and leave
/// <see cref="BlockEntityFurnaceCore.IsChoked"/> permanently unreachable.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnaceCold : BlockEntityShaftFurnace { }
