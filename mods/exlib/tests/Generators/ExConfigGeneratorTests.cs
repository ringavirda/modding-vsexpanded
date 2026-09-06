using ExpandedLib.Config;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <c>ExConfigGenerator</c> stamps every accessor it emits with <see cref="ExConfigAccessorAttribute"/>
/// so <see cref="ExConfig.LoadAll"/> can find it by reflection - checked here against
/// <c>ExModSystemTestValues</c>, generated from <see cref="ExModSystemTestConfig"/>.
/// </summary>
public class ExConfigGeneratorTests {
  [Fact]
  public void The_generated_accessor_carries_ExConfigAccessor_naming_its_config_type() {
    var attr = typeof(ExModSystemTestValues).GetCustomAttributes(
      typeof(ExConfigAccessorAttribute),
      inherit: false
    );

    var accessor = Assert.Single(attr);
    Assert.Equal(
      typeof(ExModSystemTestConfig),
      ((ExConfigAccessorAttribute)accessor).ConfigType
    );
  }
}
