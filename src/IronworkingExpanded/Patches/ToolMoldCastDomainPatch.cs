using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Patches;

/// <summary>
/// Lets a mod-added metal be cast in a vanilla tool mold. Vanilla substitutes <c>{metal}</c> into the
/// mold's drop template from <c>Collectible.LastCodePart()</c> and keeps the template's domain, so the
/// plate mold's <c>game:metalplate-{metal}</c> resolves to <c>game:metalplate-castiron</c> - an item that
/// does not and must not exist (a <c>block/metal</c> worldproperty variant would make cast iron
/// anvil-forgeable; see <see cref="Items.CastIronItemDefinitions"/>). This prefix rehomes the drop into
/// the metal's <see cref="MetalDef.CastDomain"/> instead, yielding <c>iwex:metalplate-castiron</c>.
/// <para>
/// Patched on <c>GetMoldedStacks</c> - the single chokepoint every casting path funnels through:
/// <c>GetStateAwareMoldedStacks</c> (right-click retrieval, and smex's replacement interact flow) and
/// <c>CanReceive</c> (the gate on pouring from a crucible/ladle at all). Without the latter a vanilla
/// pour would refuse cast iron outright, not merely fail to hand it back.
/// </para>
/// <para>
/// A PREFIX, not a postfix: vanilla's <c>stackFromCode</c> logs a resolve warning per failed template,
/// so a postfix would spam the log on every cast-iron pour before we ever saw the result. Metals with no
/// <c>CastDomain</c> - i.e. every vanilla metal - fall straight through to the original method.
/// </para>
/// <para>
/// Note smex's <c>ToolMoldPatches</c> also patches the tool mold. The two target different methods so
/// Harmony does not conflict; smex owns the interaction flow, this owns drop resolution.
/// </para>
/// </summary>
[HarmonyPatch]
public static class ToolMoldCastDomainPatch
{
  [HarmonyPrefix]
  [HarmonyPatch(
    typeof(BlockEntityToolMold),
    nameof(BlockEntityToolMold.GetMoldedStacks)
  )]
  public static bool GetMoldedStacksPrefix(
    BlockEntityToolMold __instance,
    ItemStack fromMetal,
    ref ItemStack[]? __result
  )
  {
    AssetLocation? metal = fromMetal?.Collectible?.Code;
    if (metal == null || MetalRegistry.CastDomainOf(metal) == null)
      return true;

    IWorldAccessor world = __instance.Api.World;
    var molded = new List<ItemStack>();

    foreach (JsonItemStack template in ExMoldDrops.Templates(__instance.Block))
    {
      if (template.Code == null)
        continue;

      template.Code = MetalRegistry.CastProductOf(template.Code, metal);
      // Silence the resolve warning: a mold whose drop this metal simply has no product for is a
      // normal "not castable here" answer, which CanReceive turns into a refused pour.
      if (
        !template.Resolve(
          world,
          "cast-domain tool mold drop",
          printWarningOnError: false
        )
      )
        continue;

      ItemStack? stack = template.ResolvedItemstack;
      if (stack == null)
        continue;

      // Carry the melt's temperature into the casting, exactly as vanilla does.
      ItemStack? content = __instance.MetalContent;
      if (content != null)
        stack.Collectible.SetTemperature(
          world,
          stack,
          content.Collectible.GetTemperature(world, content)
        );

      molded.Add(stack);
    }

    __result = molded.ToArray();
    return false;
  }
}
