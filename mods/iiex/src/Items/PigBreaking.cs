namespace IronIndustryExpanded.Items;

/// <summary>
/// The mass-conserving arithmetic behind breaking a cast pig on the anvil under a helve hammer. A pig is
/// placed as a work item of <see cref="PigVoxels"/> metal voxels standing for its
/// <see cref="ItemPig.PigUnits"/> units; each helve hit removes voxels, and the freed units are paid out as
/// whole chunks (25 u) and bits (5 u), with the sub-bit remainder carried on the work item to the next hit.
/// Pure and deterministic. The anvil wiring lives in
/// <see cref="IronIndustryExpanded.Patches.AnvilPigBreakingPatches"/>.
/// </summary>
public static class PigBreaking {
  /// <summary>Metal voxels a fresh pig fills on the anvil, chosen so units-per-voxel is exact: 375 u over
  /// 150 voxels is 2.5 u/voxel, so the 10-voxel recipe leftover is one 25 u chunk and the 140 shed voxels
  /// are fourteen more, fifteen chunks in all. Must be changed together with
  /// <see cref="ItemPig.PigUnits"/> so that <see cref="UnitsPerVoxel"/> stays at 2.5 u/vx³, the mod's
  /// density rule.</summary>
  public const int PigVoxels = 150;

  /// <summary>The cold-workable work item a placed pig becomes (see <see cref="ItemPigWorkItem"/>). Still
  /// tagged with <see cref="MarkerKey"/> so the helve patch acts only on it.</summary>
  public const string WorkItemCode = "iiex:pigworkitem-iron";

  /// <summary>Stack-attribute flag marking a work item as a pig being broken (so the helve patch acts only
  /// on it, never on ordinary iron smithing). Also carries the sub-bit unit remainder between hits.</summary>
  public const string MarkerKey = "iwexpigbreak";

  /// <summary>Units each removed voxel frees (375 / 150 = 2.5). The mod's density rule, stated only
  /// here.</summary>
  public static float UnitsPerVoxel => ItemPig.PigUnits / (float)PigVoxels;

  /// <summary>
  /// Adds the units freed by <paramref name="voxelsRemoved"/> to <paramref name="remainder"/>, then pays
  /// out as many whole chunks (25 u) and bits (5 u) as have accrued, leaving the sub-bit remainder in
  /// <paramref name="remainder"/> for the next hit. Over a full break the payout equals the removed voxels'
  /// units to within one sub-bit remainder.
  /// </summary>
  public static (int Chunks, int Bits) Emit(
    int voxelsRemoved,
    ref float remainder
  ) {
    if (voxelsRemoved > 0)
      remainder += voxelsRemoved * UnitsPerVoxel;

    int chunks = (int)(remainder / ItemPig.ChunkUnits);
    remainder -= chunks * ItemPig.ChunkUnits;
    int bits = (int)(remainder / ItemPig.BitUnits);
    remainder -= bits * ItemPig.BitUnits;
    return (chunks, bits);
  }
}
