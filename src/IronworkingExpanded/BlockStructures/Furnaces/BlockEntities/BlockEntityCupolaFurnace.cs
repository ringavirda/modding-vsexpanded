using System.Collections.Generic;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cupola furnace: the blast furnace's machine (<see cref="BlockEntityBlastFurnace"/>) run as a
/// scrap re-melter. It inherits the entire fire/melt/drain/residue path unchanged and only swaps the
/// facts that make it a cupola rather than a blast furnace, all as data:
/// <list type="bullet">
/// <item>it accepts <b>remelt</b> burden only (scrap metal + flux + coke), never ore burden - the
/// wrong family still lights and burns the shaft, but never renders molten metal;</item>
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
public class BlockEntityCupolaFurnace : BlockEntityBlastFurnace
{
  #region Charge family

  // The cupola re-melts scrap: it burns remelt burden only. Ore burden charged into it (by hand, hopper
  // or a mis-drained mixer) burns out in the shaft but never renders cast iron - the mirror of the blast
  // furnace refusing remelt burden. Cached array per the "AllowedX must be a cached prop" convention.
  private static readonly string[] _acceptedRemelt = [Burden.FamilyRemelt];

  protected override IReadOnlyList<string>? AcceptedFamilies => _acceptedRemelt;

  #endregion

  #region Product identity

  // The lower tap pours cast iron (not iron), the pool freezes into the solidified-cast-iron block, and
  // the HUD names its molten pool as cast iron. Everything else about the drain/residue is inherited.
  protected override string MetalProductCode => "castiron";

  protected override string MoltenProductInfoLangKey =>
    IwexLang.CupolaInfoMoltencastiron;

  protected override AssetLocation SolidProductBlock { get; } =
    new("iwex", "solidifiedcastiron");

  #endregion

  #region Tunables (its own, slower cadence)

  protected override float MeltingPoint => IwexValues.CupolaCastIronMeltingPoint;
  protected override int MaxFuelBurnTime => IwexValues.CupolaMaxFuelBurnTime;
  protected override float MeltStartDelay => IwexValues.CupolaMeltStartDelay;
  protected override float MeltIntervalSec => IwexValues.CupolaMeltIntervalSec;
  protected override float TuyereIntakeVolume =>
    IwexValues.CupolaTuyereIntakeVolume;
  protected override int BlastMixRequiredToFire =>
    IwexValues.CupolaMixRequiredToFire;

  protected override float ProductPerMeltCycle =>
    IwexValues.CupolaCastIronPerMeltCycle;
  protected override float SlagYieldPerMeltCycle =>
    IwexValues.CupolaSlagPerMeltCycle;
  protected override int ChargeConsumedPerMeltCycle =>
    IwexValues.CupolaBlastMixPerMeltCycle;
  protected override float MaxMoltenProduct =>
    IwexValues.CupolaMaxMoltenCastIron;
  protected override float MaxMoltenSlagPool => IwexValues.CupolaMaxMoltenSlag;

  #endregion

  #region Structure geometry

  // Every cell below is a structure-local offset in the core's north frame - the same frame its
  // BlockCupolaFurnaceCore layout is drawn in (Origin(-1,-1)), so each must land on the matching glyph:
  // the tuyere on 'Y', the taps on 'T', the shaft column on 'c'. FurnaceGeometryTests pins that.

  /// <summary>A single tuyere on the north face of the hearth (layout 'Y' at layer 1).</summary>
  protected override Vec3i[] TuyereCells => [new(0, 1, -1)];

  /// <summary>No exhaust outlets: the cupola's open top is its stack, so - like the cold blast furnace -
  /// it has no pipe outlet cell. Registering outlet cells regardless would re-scan brick every lit tick
  /// and pin <see cref="BlockEntityFurnaceCore.IsChoked"/> unreachable.</summary>
  protected override Vec3i[] GasOutletCells => [];

  /// <summary>The lower tap, west side of layer 1 (layout 'T') - drains molten cast iron.</summary>
  protected override Vec3i MetalTapCell => new(-1, 1, 0);

  /// <summary>The upper tap, east side of layer 2 (layout 'T') - drains slag.</summary>
  protected override Vec3i SlagTapCell => new(1, 2, 0);

  /// <summary>The shaft is a single column (layout 'c' at (0,1..5,0)).</summary>
  protected override Vec3i ShaftMin => new(0, 1, 0);
  protected override Vec3i ShaftMax => new(0, 5, 0);
  protected override Vec3i ShaftCentre => new(0, 3, 0);

  /// <summary>The one chargeable cell on the lowest level of the shaft - where the molten pool freezes.</summary>
  protected override Vec3i[] SolidifyCells => [new(0, 1, 0)];

  #endregion
}
