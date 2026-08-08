using System;
using System.Collections.Generic;
using ExpandedLib.Materials;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cupola furnace: the blast furnace's machine (<see cref="BlockEntityShaftFurnace"/>) run as a
/// scrap re-melter. It inherits the entire fire/melt/drain/residue path unchanged and only swaps the
/// facts that make it a cupola rather than a blast furnace, all as data:
/// <list type="bullet">
/// <item>it charges <b>metal directly</b> - pig, pig chunks, pig bits and iron/steel scrap (the
/// <c>scrap</c> material role) - never prepared burden; ore burden charged into it by hand or by hopper
/// still lights and burns the shaft, but never renders molten metal;</item>
/// <item>it melts that burden into <b>cast iron</b> (1200 °C, well below wrought iron's line), draining
/// cast iron out the lower tap and slag out the upper - the same two-tap arrangement, relabelled;</item>
/// <item>it is a narrower furnace: a single tuyere, a single-column shaft, and no exhaust outlets (its
/// open top is the stack, exactly as the cold blast furnace's is);</item>
/// <item>it runs on its own, slower <c>Cupola*</c> tunables (see <see cref="IwexConfig"/>): ~2 cupolas
/// keep pace with one blast furnace's pig-iron output.</item>
/// </list>
/// The extinguish residue is the core's, shared with every furnace: the molten pool freezes across the
/// hearth floor - here into solid <b>cast iron</b> - and the remaining burden is burned out to
/// salvageable spent charge, never slag.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCupolaFurnace : BlockEntityShaftFurnace
{
  /// <summary>
  /// The cupola is a <b>remelt</b> furnace, so its pile is measured in metal <b>units</b>, not items:
  /// coke goes in as items first, then a 5 u bit, a 25 u chunk or a 375 u pig each contribute their own
  /// units toward this cap. Set at <b>3 000 u</b> - 8 pigs of metal against 8 bands of coke.
  /// The bare 16 x 375 = 6 000 is the <i>no-coke</i> figure, and coke takes about half a cupola's pile
  /// height (its ratio to metal is ~1:8-1:10 by weight, but coke is bulky enough that a tonne of it
  /// occupies roughly the space of the ten tonnes it melts).
  /// </summary>
  public override int ChargeUnitsPerBlock =>
    IwexValues.CupolaChargeMetalUnitsPerBlock;

  #region What a cupola eats

  /// <summary>
  /// <b>The cupola charges metal directly - pig, pig chunks, pig bits and iron/steel scrap - and never
  /// prepared burden.</b> It is the one furnace in the mod whose charge is <em>items of metal</em> rather
  /// than a blended feed, which is also how a cupola worked in life: you shovelled in scrap and coke in
  /// alternating rounds and lit it.
  /// <para>
  /// Charging the metal itself avoids an intermediate remelt-burden item, which would need a producing
  /// machine of its own and would tie cast iron's survival source to that machine.
  /// </para>
  /// <para>
  /// <b>Both seams are overridden, and that is not belt-and-braces.</b> The core states the rule itself:
  /// a furnace that overrides <see cref="IsChargeItem"/> and not <see cref="IsChargeCode"/> gates its
  /// hand-placed piles and its layered columns <em>differently</em>, so the same charge is accepted through
  /// one route and refused through the other - <b>with nothing failing</b>, because both answers are
  /// individually plausible. A stack arrives through the hopper; a column holds a material code and never a
  /// stack. <c>CupolaChargeIdentityTests</c> pins the two together.
  /// </para>
  /// </summary>
  /// <para>
  /// <b>The <c>|| IsFuelCode</c> half is not optional and it cannot be dropped.</b> These override
  /// <see cref="BlockEntityShaftFurnace"/>'s <c>burden || fuel</c>, and only the <em>burden</em> half is
  /// being replaced: a shaft's coke rounds are charge too, and <c>ReadChargeMix</c> <b>skips any segment
  /// that is not charge</b>. Written as scrap-only, the cupola's coke stops counting toward
  /// its fullness, its carbon and its heat balance - and nearly the whole test suite stays green over it.
  /// </para>
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

  // Families are a property of burden, and this furnace takes no burden at all - a family gate here
  // would gate a charge that can never carry a family, refusing everything.

  #endregion

  #region Product identity

  // The lower tap pours cast iron (not iron), the pool freezes into the solidified-cast-iron block, and
  // the HUD names its molten pool as cast iron. Everything else about the drain/residue is inherited.
  protected override string MetalProductCode => "castiron";

  protected override string MoltenProductInfoLangKey =>
    IwexLang.CupolaInfoMoltencastiron;

  protected override AssetLocation SolidProductBlock { get; } =
    new("iwex", "hearthmetal-castiron");

  #endregion

  #region Tunables (its own, slower cadence)

  protected override float MeltingPoint => IwexValues.CupolaCastIronMeltingPoint;
  protected override float TuyereIntakeVolume =>
    IwexValues.CupolaTuyereIntakeVolume;

  // The cupola has no cadence tunables of its own: `MaxFuelBurnTime`, `MeltStartDelay` and
  // `MeltIntervalSec` are all sealed on the shaft branch - a shaft's campaign ends when its carbon does,
  // its soak is the counter-current warm-through, and its melt cadence is the descent - so there is
  // nothing for a `Cupola*` cadence key to override.
  //
  // The cupola is still slower than the blast furnace, for a better reason than a key: it holds ~3 000
  // metal units per block against an ore shaft's 32 items, so `ChargeUnitScale` stretches its carbon rate
  // over a far larger charge. The pacing is geometry, not a constant. `ChargeCapacityUnits` is likewise
  // sealed on the shaft branch - a cupola's capacity is its own chargeable cells times its own
  // 3 000-unit quantum, which the branch expression already answers.

  /// <summary>
  /// <b>Flat, and deliberately not scaled by an ore share.</b> A cupola <b>remelts</b> rather than
  /// reduces: what goes in is already metal, so there is no ore fraction in its charge for a recovery
  /// ladder to be stated against. Reading <see cref="IwexValues.BfIronPerOreUnit"/> here would apply a
  /// blast furnace's <em>reduction</em> yield to metal that was never ore, and the guard-rail that watches
  /// that number is about ore recovery only.
  /// <para>
  /// The pair is kept because their <em>ratio</em> is the meaningful figure - 60 units of cast iron and
  /// 8 of slag per 12 charge - and expressing it per unit here preserves both exactly.
  /// </para>
  /// </summary>
  protected override float ProductPerUnit(BurdenMix mix) =>
    IwexValues.CupolaCastIronPerMeltCycle
    / Math.Max(1, IwexValues.CupolaBlastMixPerMeltCycle);

  protected override float SlagPerUnit(BurdenMix mix) =>
    IwexValues.CupolaSlagPerMeltCycle
    / Math.Max(1, IwexValues.CupolaBlastMixPerMeltCycle);

  // There is no per-cupola melt rate: production is metered by the carbon burned
  // (`BfBurdenPerCarbonUnit`), and the cupola has nothing to say about that ratio that its charge does
  // not already say - it is slower than a blast furnace because its single tuyere burns less carbon,
  // which is a fact about its drawing rather than a constant.

  protected override float MaxMoltenProduct =>
    IwexValues.CupolaMaxMoltenCastIron;
  protected override float MaxMoltenSlagPool => IwexValues.CupolaMaxMoltenSlag;

  #endregion

  #region Structure geometry

  // The one cell below is a structure-local offset in the core's north frame - the same frame its
  // BlockCupolaFurnaceCore layout is drawn in (Origin(-1,-1)), so it must land on the matching glyph: the
  // shaft column on 'c'. FurnaceGeometryTests pins that.
  //
  // Its single tuyere ('Y' at (0,1,-1)), its lack of any exhaust outlet - the cupola's open top is its
  // stack - its one-cell crucible, both of its drains (cast iron out low to the west on 'T', slag off the
  // bath to the east on 'S') and its shaft box are all off the drawing, through CellRole.Tuyere,
  // CellRole.GasOutlet, CellRole.Pool, CellRole.MetalTap, CellRole.SlagTap and CellRole.Chargeable - an
  // absence the layout states needs no override to restate.
  //
  // The shaft box is the bounding box of the five 'c' cells the drawing marks Chargeable - and on this
  // furnace, uniquely, the box happens to be the charge volume rather than merely containing it.

  /// <summary>The middle of the single column - a geometric point, which is why it is still declared.</summary>
  protected override Vec3i ShaftCentre => new(0, 3, 0);

  #endregion
}
