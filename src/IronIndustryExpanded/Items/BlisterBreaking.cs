using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The cold branch of the blister-steel fork: a vanilla <c>game:ingot-blistersteel</c> too cold for the
/// anvil is crushed under a helve hammer into crucible-furnace charge instead. Hot blister keeps vanilla's
/// behaviour and forges into shear steel, so the two routes never overlap - see
/// <see cref="IronIndustryExpanded.Patches.AnvilBlisterBreakingPatches"/>, which decides the fork, and
/// docs/design/machines/crucible-furnace.md for why the feedstock is prepared this way.
/// </summary>
/// <remarks>
/// Mass is exact rather than approximate: 100 u in, 100 u out, as three shed chunks and the five bits the
/// finished shape hands back. Pure and deterministic, so the arithmetic is testable without an anvil.
/// </remarks>
public static class BlisterBreaking {
  /// <summary>The ingot this chain consumes. Vanilla's own, unqualified nowhere: a bare
  /// <c>ingot-blistersteel</c> would resolve against whatever domain asked.</summary>
  public const string IngotCode = "game:ingot-blistersteel";

  /// <summary>Material units in one vanilla ingot. Vanilla states it twenty times over: a
  /// <c>metalbit</c> smelts at <c>smeltedRatio: 20</c> to one ingot, which is what puts
  /// <see cref="BitUnits"/> at 5.</summary>
  public const int IngotUnits = 100;

  /// <summary>Units in one crushed chunk (25 u) and one vanilla blister-steel bit (5 u). The bit is
  /// vanilla's own denomination; the chunk is five of them, the same size the pig chain uses.</summary>
  public const int ChunkUnits = 25;

  public const int BitUnits = 5;

  /// <summary>Metal voxels a cold ingot fills on the anvil, chosen so units-per-voxel is exact:
  /// <see cref="IngotUnits"/> over 40 voxels is 2.5 u/vx³, the mod's density rule, the same rate the pig
  /// is priced at.</summary>
  public const int IngotVoxels = 40;

  /// <summary>The cold-workable work item a placed ingot becomes
  /// (see <see cref="ItemBlisterWorkItem"/>).</summary>
  public const string WorkItemCode = "iiex:blisterworkitem-blistersteel";

  /// <summary>What the helve sheds, and what the finished shape hands back. The bit is vanilla's, so the
  /// chain pays out in a denomination the player already has a use for.</summary>
  public const string ChunkCode = "iiex:blisterchunk";

  public const string BitCode = "game:metalbit-blistersteel";

  /// <summary>Stack-attribute flag marking a work item as an ingot being crushed, so the helve patch acts
  /// only on it and never on ordinary steel smithing. Also carries the sub-chunk unit remainder between
  /// hits.</summary>
  public const string MarkerKey = "iiexblistercrush";

  /// <summary>The smithing recipe's stable identifier, and its golden's name.</summary>
  public const string RecipeCode = "iiexblistercrush";

  /// <summary>
  /// The recipe's name, which is the string the fork is read off: the patch keeps this recipe for a cold
  /// ingot and drops it for a hot one.
  /// </summary>
  /// <remarks>
  /// It must not be <c>blistersteel</c> or <c>plate</c>. Vanilla's <c>ItemWorkItem</c> grants helve
  /// workability by matching the selected recipe's name against exactly those two, so either spelling
  /// would make vanilla work items follow this recipe as well.
  /// </remarks>
  public const string RecipeName = "blistercrush";

  /// <summary>Anvil voxel the crushed block starts at, and how far it runs on each axis. Sized and placed
  /// so it covers the recipe's own shape exactly, with <see cref="ShedVoxels"/> left over to shed.</summary>
  public const int OriginX = 5;

  public const int OriginZ = 6;
  public const int Width = 5;
  public const int Layers = 2;
  public const int Depth = 4;

  /// <summary>Voxels the finished shape keeps, worth <see cref="ChunkUnits"/>: what the anvil hands back
  /// as five bits.</summary>
  public const int ShapeVoxels = 10;

  /// <summary>Voxels the helve sheds, worth three chunks.</summary>
  public const int ShedVoxels = IngotVoxels - ShapeVoxels;

  /// <summary>Units each removed voxel frees (100 / 40 = 2.5), the mod's density rule.</summary>
  public static float UnitsPerVoxel => IngotUnits / (float)IngotVoxels;

  /// <summary>
  /// Adds the units freed by <paramref name="voxelsRemoved"/> to <paramref name="remainder"/> and pays out
  /// as many whole chunks as have accrued, leaving the sub-chunk part in <paramref name="remainder"/> for
  /// the next hit.
  /// </summary>
  /// <remarks>
  /// Chunks only, where the pig pays chunks and bits both. A helve hit sheds one voxel, so a payout that
  /// also made change would settle every second hit and the run would come out as twenty bits rather than
  /// the three chunks and five bits the process is priced at.
  /// </remarks>
  public static int Emit(int voxelsRemoved, ref float remainder) {
    if (voxelsRemoved > 0)
      remainder += voxelsRemoved * UnitsPerVoxel;

    int chunks = (int)(remainder / ChunkUnits);
    remainder -= chunks * ChunkUnits;
    return chunks;
  }

  /// <summary>Whether <paramref name="stack"/> is the vanilla blister-steel ingot this chain forks on.</summary>
  public static bool IsBlisterIngot(ItemStack? stack) =>
    stack?.Collectible?.Code?.ToString() == IngotCode;

  /// <summary>
  /// Whether a placement falls to the crushing route. Vanilla decides it: an ingot its own
  /// <c>TryPlaceOn</c> declined is one too cold for an anvil, and that refusal is the whole fork. Nothing
  /// here reads a temperature, so the two routes cannot both open on one ingot.
  /// </summary>
  public static bool Crushes(
    ItemStack? ingot,
    bool vanillaAccepted,
    bool anvilOccupied
  ) => !vanillaAccepted && !anvilOccupied && IsBlisterIngot(ingot);

  /// <summary>
  /// Voxels one helve hit shed, given the metal on the anvil before and after it.
  /// </summary>
  /// <remarks>
  /// The hit that finishes the shape is counted differently, and has to be. Vanilla completes the recipe
  /// from inside the hit, blanking the anvil, so <paramref name="after"/> reads zero and the naive
  /// difference would pay out the whole remaining block. What actually left is one voxel; the rest walked
  /// off as the recipe's own output, which is <paramref name="shapeVoxels"/>.
  /// </remarks>
  public static int Shed(
    int before,
    int after,
    int shapeVoxels,
    bool finished
  ) => before - (finished ? shapeVoxels : after);

  /// <summary>
  /// Whether a smithing recipe stays on offer for a blister ingot. Exactly one of the two routes survives
  /// the fork: the crushing recipe when vanilla will not work the ingot, vanilla's own shear-steel recipe
  /// when it will. Leaving both would let a cold ingot be forged, which is the branch this chain exists
  /// not to open.
  /// </summary>
  public static bool Offers(bool crushingRecipe, bool vanillaWouldWork) =>
    crushingRecipe != vanillaWouldWork;

  /// <summary>
  /// Lays the crushed ingot out on the anvil: a solid <see cref="Width"/> x <see cref="Layers"/> x
  /// <see cref="Depth"/> block of metal, <see cref="IngotVoxels"/> voxels, positioned so its lowest layer
  /// covers the smithing recipe's shape and nothing else has to be conjured.
  /// </summary>
  /// <remarks>
  /// The position is not free. A smithing recipe is centred on the anvil and transposed as it is laid out,
  /// so this block has to be placed against where the recipe actually lands rather than where its pattern
  /// reads - a block that misses it makes the helve create metal out of nothing to fill the gap.
  /// </remarks>
  public static void CreateVoxels(ref byte[,,] voxels) {
    voxels = new byte[16, 6, 16];
    for (int x = 0; x < Width; x++)
      for (int y = 0; y < Layers; y++)
        for (int z = 0; z < Depth; z++)
          voxels[OriginX + x, y, OriginZ + z] = (byte)EnumVoxelMaterial.Metal;
  }
}
