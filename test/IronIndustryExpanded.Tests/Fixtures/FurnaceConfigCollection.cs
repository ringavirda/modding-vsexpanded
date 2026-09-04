using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Serializes every test class that writes furnace config or reads a computed heat balance.
/// <c>IiexValues</c> is a process-wide static and xUnit parallelises test classes, so a class that
/// lowers a config value changes what a concurrent class observes; restoring it in a <c>finally</c>
/// does not close that window. Only classes that join the collection are ordered, so a class that
/// starts reading <c>ComputeHeatBalance</c> must be added to it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class FurnaceConfigCollection {
  public const string Name = "IiexFurnaceConfig";
}
