using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Serializes every test class that mutates <see cref="SteelmakingExpanded.Molds.MoldGating"/>.
/// That gate is process-global static state (the config-driven "this tool mold is disabled" set), and
/// xUnit runs test classes in parallel by default - so two classes toggling <c>plate</c> at the same
/// time make each other read the wrong value. The symptom is an intermittent
/// <c>MoldRemovalTests.Only_disabled_mold_variants_are_listed_for_removal</c> failure (the removal
/// list comes back empty because another class re-enabled the variant mid-assertion).
/// <para>
/// Members: <see cref="MoldGatingTests"/>, <see cref="MoldRemovalTests"/>,
/// <see cref="MoldGatePedestalPurgeTests"/>. Add any new gate-mutating class here too.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class MoldGatingCollection
{
  public const string Name = "MoldGating";
}
