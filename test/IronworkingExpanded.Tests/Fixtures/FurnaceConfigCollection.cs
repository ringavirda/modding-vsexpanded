using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Serializes every test class that <b>writes</b> furnace config or <b>reads</b> a computed heat balance.
/// <c>IwexValues</c> is a process-wide static, and xUnit parallelises test classes: a class that turns
/// <c>BfCombustionBaseTemp</c> down to 0 to prove the ambient floor holds would, running alongside the HUD
/// tests, briefly make a perfectly good furnace produce cold and flip which lines it prints. Restoring the
/// value in a <c>finally</c> does not help - the window is what races.
/// <para>
/// Joining is what serializes: a collection only orders the classes that opt in, so a class that starts
/// reading <c>ComputeHeatBalance</c> must be added here too. See the same pattern for the mold gate
/// (<c>SteelmakingExpanded.Tests/Blocks/Molds/MoldGatingCollection.cs</c>) and for
/// <c>ExMeasure</c> in the exlib suite.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class FurnaceConfigCollection
{
  public const string Name = "IwexFurnaceConfig";
}
