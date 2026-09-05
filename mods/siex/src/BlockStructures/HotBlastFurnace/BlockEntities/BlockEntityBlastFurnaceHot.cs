using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

namespace SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// The hot blast furnace. It carries no behaviour or tunables of its own: it is the same machine as the
/// cold furnace, differing in its layout (a shaft carrying exhaust outlets and a sealed bell top) and in
/// the cowper on its blast line, which delivers preheated air at the tuyeres. The heat balance in
/// <see cref="IronIndustryExpanded.BlockStructures.Furnaces.BlockEntityFurnaceCore.ComputeHeatBalance"/>
/// reads both and produces the higher melt line and faster render; on cold air, or below the
/// blast-pressure threshold, the furnace behaves as a cold one. It is a distinct registered type because
/// the two anchors bind distinct block entity classes and distinct multiblock layouts.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnaceHot : BlockEntityShaftFurnace { }
