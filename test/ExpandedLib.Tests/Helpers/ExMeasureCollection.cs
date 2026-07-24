using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Serializes every test class that touches <see cref="ExpandedLib.Helpers.ExMeasure.System"/> - the
/// process-global metric/imperial display setting. xUnit runs test classes in parallel, so a class that
/// flips the unit system will corrupt any class that *reads* it, even one that never writes it.
/// <para>
/// Members: <see cref="ExMeasureTests"/> (writes it), <see cref="MoltenMetalTests"/> (reads it, through
/// <c>MoltenMetal</c>'s temperature formatter). The bare <c>[Collection("ExMeasure")]</c> that used to
/// sit on the writer alone was not enough - a collection only serializes classes that <em>join</em> it,
/// and it went unnoticed while the two classes lived in different assemblies. Add any new class that
/// formats a measured value here too.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ExMeasureCollection
{
  public const string Name = "ExMeasure";
}
