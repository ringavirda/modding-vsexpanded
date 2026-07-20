using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// The hot blast furnace. Deliberately empty: it is the same machine as the cold furnace, and every
/// heat tunable it used to restate was an identical copy of iwex's. What makes it the hot one is its
/// layout - a shaft that carries exhaust outlets and a sealed bell top - and the cowper on its blast
/// line, which arrives preheated at the tuyeres. The heat balance in
/// <see cref="IronworkingExpanded.BlockStructures.Furnaces.BlockEntityFurnaceCore.ComputeHeatBalance"/>
/// reads both and produces the higher melt line and faster render on its own; run the same furnace on
/// cold air, or below the blast-pressure threshold, and it behaves as a cold furnace again.
/// <para>
/// It stays a distinct registered type because the two anchors bind distinct block entity classes
/// (<c>iwex.BlockEntityBlastFurnaceCold</c> / <c>smex.BlockEntityBlastFurnaceHot</c>) and carry
/// distinct multiblock layouts - not because it has distinct behaviour to hold.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnaceHot : BlockEntityBlastFurnace { }
