using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// Makes any machine entity that gates on a <see cref="BEBehaviorRightClickConstructable"/> (boiler,
/// engine) read as fully constructed. The behavior's <c>IsComplete</c> is
/// <c>rcc.CurrentCompletedStage == rcc.Stages.Length - 1</c> and isn't virtual, so a real instance is
/// built with a single, already-completed stage and dropped into the entity's private <c>_rcc</c>
/// field. Re-apply after a real <c>Initialize</c> (which re-reads <c>_rcc</c> from the absent
/// behaviors and clears it).
/// </summary>
internal static class RccFake
{
  public static void Complete(BlockEntity be)
  {
    var construction = new RightClickConstruction
    {
      Stages = [new ConstructionStage()],
      CurrentCompletedStage = 0,
    };
    var rcc = new BEBehaviorRightClickConstructable(be);
    ReflectionHelpers.SetField(rcc, "rcc", construction);
    ReflectionHelpers.SetField(be, "_rcc", rcc);
  }
}
