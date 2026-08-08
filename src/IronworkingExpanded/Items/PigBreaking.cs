namespace IronworkingExpanded.Items;

/// <summary>
/// The mass-conserving arithmetic behind breaking a cast pig on the anvil under a helve hammer. A pig is
/// placed as a work item of <see cref="PigVoxels"/> metal voxels standing for its
/// <see cref="ItemPig.PigUnits"/> units; each helve hit removes voxels, and the freed units are
/// paid out as whole chunks (25u) and bits (5u), the sub-bit remainder carried on the work item to the
/// next hit. Pure + deterministic so the conservation is unit-tested; the anvil wiring lives in
/// <see cref="IronworkingExpanded.Patches.AnvilPigBreakingPatches"/>.
/// </summary>
public static class PigBreaking
{
  /// <summary>Metal voxels a fresh pig fills on the anvil - chosen so units-per-voxel is exact: a 375u pig
  /// over 150 voxels is 2.5 u/voxel, so a 10-voxel recipe leftover is one 25u pigchunk and the 140 shed
  /// voxels are fourteen more, fifteen chunks = 375u.
  /// <para>
  /// It moves with <see cref="ItemPig.PigUnits"/> and always must: 2.5 u/vx³ is the mod's <b>one</b> density
  /// rule, and <see cref="UnitsPerVoxel"/> is the only place in the codebase that expresses it. Pinning the
  /// voxel count while the mass moves would make an anvil voxel of pig iron denser than a pig-iron voxel
  /// anywhere else - and would quietly hand the player a cheaper break per unit of iron.
  /// </para></summary>
  public const int PigVoxels = 150;

  /// <summary>The dedicated cold-workable pig work item a placed pig becomes (see
  /// <see cref="ItemPigWorkItem"/>) - still tagged with <see cref="MarkerKey"/> so the helve patch acts only
  /// on it. A real item of ours (not the reused vanilla iron work item), so it renders as iron voxels but
  /// works cold and keeps other mods' iron-work patches off the pig.</summary>
  public const string WorkItemCode = "iwex:pigworkitem-iron";

  /// <summary>Stack-attribute flag marking a work item as a pig being broken (so the helve patch acts only
  /// on it, never on ordinary iron smithing). Also carries the sub-bit unit remainder between hits.</summary>
  public const string MarkerKey = "iwexpigbreak";

  /// <summary>Units each removed voxel frees (<c>375 / 150 = 2.5</c>) - the mod's density rule, and the only
  /// place the codebase states it.</summary>
  public static float UnitsPerVoxel =>
    ItemPig.PigUnits / (float)PigVoxels;

  /// <summary>
  /// Adds the units freed by <paramref name="voxelsRemoved"/> to <paramref name="remainder"/>, then pays
  /// out as many whole chunks (25u) and bits (5u) as have accrued, leaving the sub-bit remainder for the
  /// next hit. Over a full break the payout equals the removed voxels' units (to within one sub-bit
  /// crumb), so no matter is created.
  /// </summary>
  public static (int Chunks, int Bits) Emit(int voxelsRemoved, ref float remainder)
  {
    if (voxelsRemoved > 0)
      remainder += voxelsRemoved * UnitsPerVoxel;

    int chunks = (int)(remainder / ItemPig.ChunkUnits);
    remainder -= chunks * ItemPig.ChunkUnits;
    int bits = (int)(remainder / ItemPig.BitUnits);
    remainder -= bits * ItemPig.BitUnits;
    return (chunks, bits);
  }
}
