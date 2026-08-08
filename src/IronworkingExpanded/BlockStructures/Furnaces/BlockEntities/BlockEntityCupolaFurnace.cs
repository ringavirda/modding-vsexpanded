using System;
using System.Collections.Generic;
using ExpandedLib.Materials;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cupola furnace: <see cref="BlockEntityShaftFurnace"/> run as a scrap re-melter, inheriting the whole
/// fire/melt/drain/residue path and overriding only data. It charges metal directly (pig, pig chunks, pig
/// bits and iron/steel scrap, the <c>scrap</c> material role) rather than prepared burden, melts it to cast
/// iron at 1200 °C, drains cast iron out the lower tap and slag out the upper, and freezes its pool into
/// solid cast iron when extinguished. Ore burden charged into it burns but renders no molten metal. One
/// tuyere, a single-column shaft, no exhaust outlet (its open top is the stack), and its own slower
/// <c>Cupola*</c> tunables (see <see cref="IwexConfig"/>). See docs/design/machines/cupola.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCupolaFurnace : BlockEntityShaftFurnace {
  /// <summary>
  /// Charge capacity per block in metal units rather than items, the cupola being a remelt furnace: a 5 u
  /// bit, a 25 u chunk or a 375 u pig each contribute their own units. 3 000 u is 8 pigs of metal against
  /// 8 bands of coke, coke taking about half a cupola's pile height.
  /// </summary>
  public override int ChargeUnitsPerBlock =>
    IwexValues.CupolaChargeMetalUnitsPerBlock;

  #region What a cupola eats

  /// <summary>
  /// The cupola charges metal directly - pig, pig chunks, pig bits and iron/steel scrap - and never prepared
  /// burden. Both charge seams must be overridden together: this one gates hand-placed piles, which arrive
  /// as stacks, and <see cref="IsChargeCode"/> gates layered columns, which hold a material code.
  /// <c>CupolaChargeIdentityTests</c> pins the two together. The fuel clause is required as well: these
  /// replace <see cref="BlockEntityShaftFurnace"/>'s <c>burden || fuel</c>, and <c>ReadChargeMix</c> skips
  /// any segment that is not charge, so a scrap-only predicate would drop the cupola's coke from its
  /// fullness, its carbon and its heat balance.
  /// </summary>
  public override bool IsChargeItem(ItemStack? stack) =>
    IsScrapStack(stack) || IsFuelStack(stack);

  /// <inheritdoc cref="IsChargeItem"/>
  public override bool IsChargeCode(string? material) =>
    IsScrapCode(material) || IsFuelCode(material);

  private static bool IsScrapStack(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && MaterialRoleRegistry.IsRole(Roles.Scrap, code);

  private static bool IsScrapCode(string? material) =>
    !string.IsNullOrEmpty(material)
    && MaterialRoleRegistry.IsRole(Roles.Scrap, new AssetLocation(material));

  // No family gate: families are a property of burden, and this furnace takes no burden, so a gate here
  // would refuse everything.

  #endregion

  #region Product identity

  // The lower tap pours cast iron, the pool freezes into the solidified-cast-iron block, and the HUD names
  // the molten pool as cast iron. The rest of the drain/residue path is inherited.
  protected override string MetalProductCode => "castiron";

  protected override string MoltenProductInfoLangKey =>
    IwexLang.CupolaInfoMoltencastiron;

  protected override AssetLocation SolidProductBlock { get; } =
    new("iwex", "hearthmetal-castiron");

  #endregion

  #region Tunables (its own, slower cadence)

  protected override float MeltingPoint =>
    IwexValues.CupolaCastIronMeltingPoint;
  protected override float TuyereIntakeVolume =>
    IwexValues.CupolaTuyereIntakeVolume;

  // No cadence tunables of its own: `MaxFuelBurnTime`, `MeltStartDelay`, `MeltIntervalSec` and
  // `ChargeCapacityUnits` are all sealed on the shaft branch. The cupola is slower by geometry rather than
  // by a key - ~3 000 metal units per block against an ore shaft's 32 items, so `ChargeUnitScale` stretches
  // its carbon rate over a far larger charge.

  /// <summary>
  /// Flat, not scaled by an ore share: a cupola remelts rather than reduces, so its charge holds no ore
  /// fraction and <see cref="IwexValues.BfIronPerOreUnit"/>, a reduction yield, does not apply. The two
  /// constants stay a pair because their ratio - 60 units of cast iron and 8 of slag per 12 charge - is the
  /// meaningful figure.
  /// </summary>
  protected override float ProductPerUnit(BurdenMix mix) =>
    IwexValues.CupolaCastIronPerMeltCycle
    / Math.Max(1, IwexValues.CupolaBlastMixPerMeltCycle);

  protected override float SlagPerUnit(BurdenMix mix) =>
    IwexValues.CupolaSlagPerMeltCycle
    / Math.Max(1, IwexValues.CupolaBlastMixPerMeltCycle);

  // No per-cupola melt rate: production is metered by the carbon burned (`BfBurdenPerCarbonUnit`), and the
  // cupola is slower only because its single tuyere burns less of it.

  protected override float MaxMoltenProduct =>
    IwexValues.CupolaMaxMoltenCastIron;
  protected override float MaxMoltenSlagPool => IwexValues.CupolaMaxMoltenSlag;

  #endregion

  #region Structure geometry

  // The offset below is structure-local in the core's north frame, the same frame the
  // BlockCupolaFurnaceCore layout is drawn in (Origin(-1,-1)), so it must land on the shaft-column glyph
  // 'c'; FurnaceGeometryTests pins that. The single tuyere, the absent exhaust outlet, the one-cell
  // crucible, both drains and the shaft box all come off the drawing through the CellRole glyphs and need
  // no override. The shaft box is the bounding box of the five 'c' cells, which here is the charge volume.

  /// <summary>Structure-local centre of the single shaft column.</summary>
  protected override Vec3i ShaftCentre => new(0, 3, 0);

  #endregion
}
