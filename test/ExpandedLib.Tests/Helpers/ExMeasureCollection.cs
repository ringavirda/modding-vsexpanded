using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Serializes every test class that touches <see cref="ExpandedLib.Helpers.ExMeasure.System"/>, the
/// process-global metric/imperial display setting. xUnit runs test classes in parallel, so a class
/// that writes the setting corrupts any class that reads it; readers and writers alike must join this
/// collection. Members: <see cref="ExMeasureTests"/> (writes it) and <see cref="MoltenMetalTests"/>
/// (reads it through <c>MoltenMetal</c>'s temperature formatter).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ExMeasureCollection {
  public const string Name = "ExMeasure";
}
