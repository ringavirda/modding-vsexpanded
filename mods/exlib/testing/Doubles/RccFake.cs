using ExpandedLib.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Testing;

/// <summary>
/// Makes a machine entity that gates on an <see cref="ExRightClickConstructable"/> (boiler, engine)
/// read as fully constructed. <c>IsComplete</c> is not virtual - it reads
/// <c>rcc.CurrentCompletedStage == rcc.Stages.Length - 1</c> off the behavior's own field - so a real
/// behavior carrying a single, already-completed stage is planted into the private <c>_rcc</c> field.
/// Re-apply after a real <c>Initialize</c>, which re-reads <c>_rcc</c> from the absent behaviors and
/// clears it.
/// </summary>
public static class RccFake {
  public static void Complete(BlockEntity be) {
    // The single, already-completed construction state set into the behavior's "rcc" field. On 1.22
    // ExRightClickConstructable subclasses vanilla, so this is vanilla's RightClickConstruction; on
    // 1.20/1.21 vanilla has no such type, so it is exlib's ExRightClickConstruction port.
#if GAME_GE_1_22
    var construction = new RightClickConstruction
    {
      Stages = [new ConstructionStage()],
      CurrentCompletedStage = 0,
    };
#else
    var construction = new ExRightClickConstruction {
      Stages = [new ExConstructionStage()],
      CurrentCompletedStage = 0,
    };
#endif
    var rcc = new ExRightClickConstructable(be);
    ReflectionHelpers.SetField(rcc, "rcc", construction);

    // A machine that composes the ConstructedAnimator helper keeps _rcc on that helper; others hold
    // it directly. Rigs fake state without running Initialize, so the helper may be null - a bare one
    // is created (its cache key is never used off the render path) to carry the completed rcc.
    if (ReflectionHelpers.TryGetField(be, "_animator", out object? existing)) {
      object animator = existing ?? new ConstructedAnimator(be, () => "");
      ReflectionHelpers.SetField(be, "_animator", animator);
      ReflectionHelpers.SetField(animator, "_rcc", rcc);
    } else
      ReflectionHelpers.SetField(be, "_rcc", rcc);
  }
}
