using ExpandedLib.Blocks.Construction;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Testing;

/// <summary>
/// Makes any machine entity that gates on an <see cref="ExRightClickConstructable"/> (boiler,
/// engine) read as fully constructed. On 1.22 that behavior subclasses the vanilla
/// <see cref="BEBehaviorRightClickConstructable"/>, so its <c>IsComplete</c> is
/// <c>rcc.CurrentCompletedStage == rcc.Stages.Length - 1</c> off the inherited <c>rcc</c> field and
/// isn't virtual; a real instance is built with a single, already-completed stage and dropped into
/// the private <c>_rcc</c> field (typed <see cref="ExRightClickConstructable"/>). Re-apply
/// after a real <c>Initialize</c> (which re-reads <c>_rcc</c> from the absent behaviors and clears it).
/// <para>
/// The machines that share the <see cref="ConstructedAnimator"/> helper (boiler, migrated ore machines,
/// converter vessel) hold <c>_rcc</c> on that composed helper rather than on the entity itself, so the
/// fake is set there when present; the not-yet-migrated ones (the engine) still hold it directly.
/// </para>
/// </summary>
public static class RccFake
{
  public static void Complete(BlockEntity be)
  {
    // The single, already-completed construction state set into the behavior's "rcc" field. On
    // 1.22 ExRightClickConstructable subclasses vanilla, so this is vanilla's RightClickConstruction;
    // on 1.20/1.21 it is exlib's ExRightClickConstruction port (vanilla's type doesn't exist there).
#if GAME_GE_1_22
    var construction = new RightClickConstruction
    {
      Stages = [new ConstructionStage()],
      CurrentCompletedStage = 0,
    };
#else
    var construction = new ExRightClickConstruction
    {
      Stages = [new ExConstructionStage()],
      CurrentCompletedStage = 0,
    };
#endif
    var rcc = new ExRightClickConstructable(be);
    ReflectionHelpers.SetField(rcc, "rcc", construction);

    // A machine that composes the ConstructedAnimator helper keeps _rcc on that helper; a not-yet-
    // migrated one (the engine) holds it directly. The rigs fake state WITHOUT running Initialize, so
    // the helper may be absent - create a bare one (its cache key is never used off the render path)
    // and plant the completed rcc into it.
    if (ReflectionHelpers.TryGetField(be, "_animator", out object? existing))
    {
      object animator = existing ?? new ConstructedAnimator(be, () => "");
      ReflectionHelpers.SetField(be, "_animator", animator);
      ReflectionHelpers.SetField(animator, "_rcc", rcc);
    }
    else
      ReflectionHelpers.SetField(be, "_rcc", rcc);
  }
}
