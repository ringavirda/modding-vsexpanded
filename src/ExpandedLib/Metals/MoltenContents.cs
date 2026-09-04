using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Metals;

/// <summary>
/// Round-trips the metal a carried barrel or tool-mold item holds through the stack's
/// <c>blockEntityAttributes</c> tree. The tree shape matches what the vanilla tool mold and this mod's
/// barrel block entity persist, so an item placed back into the world restores intact. The tap, the
/// mold pedestal, the barrel block and the held-item info all go through these reads and writes.
/// </summary>
public static class MoltenContents {
  /// <summary>Tree key for a barrel's stored unit count.</summary>
  public const string BarrelUnitsKey = "currentUnitAmount";

  /// <summary>Tree key for a mold's stored unit count (vanilla tool-mold convention).</summary>
  public const string MoldUnitsKey = "fillLevel";

  /// <summary>
  /// Reads the metal content and unit count carried by <paramref name="itemStack"/> under
  /// <paramref name="unitsKey"/>. Returns (null, 0) when the item carries nothing.
  /// </summary>
  public static (ItemStack? content, int units) Read(
    ItemStack itemStack,
    string unitsKey,
    IWorldAccessor worldForResolve
  ) {
    if (
      itemStack.Attributes?["blockEntityAttributes"]
      is not ITreeAttribute beData
    )
      return (null, 0);

    ItemStack? content = beData.GetItemstack("contents");
    content?.ResolveBlockOrItem(worldForResolve);
    return (content, beData.GetInt(unitsKey));
  }

  /// <summary>
  /// Writes <paramref name="content"/> and <paramref name="units"/> onto
  /// <paramref name="itemStack"/> under <paramref name="unitsKey"/>. Leaves the stack untouched when
  /// there is nothing to carry.
  /// <para>
  /// For molds (<see cref="MoldUnitsKey"/>) the vanilla <c>shattered</c> and <c>meshAngle</c> keys are
  /// written too: without <c>meshAngle</c> the vanilla tool-mold's <c>hasMeshAngle</c> path triggers a
  /// server-side block exchange on placement.
  /// </para>
  /// </summary>
  public static void Write(
    ItemStack itemStack,
    string unitsKey,
    ItemStack? content,
    int units
  ) {
    if (content == null || units <= 0)
      return;

    var beData = new TreeAttribute();
    beData.SetItemstack("contents", content.Clone());
    beData.SetInt(unitsKey, units);
    if (unitsKey == MoldUnitsKey) {
      beData.SetBool("shattered", false);
      beData.SetFloat("meshAngle", 0f);
    }
    itemStack.Attributes["blockEntityAttributes"] = beData;
  }
}
