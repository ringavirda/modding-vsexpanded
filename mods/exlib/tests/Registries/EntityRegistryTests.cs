using ExpandedLib.Registries;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="EntityRegistry.DomainOf"/>'s fallback path: an assembly that declares no
/// <c>[assembly: ExDomain]</c> and was never registered resolves to the caller's own domain, which is
/// wrong whenever the type actually belongs to another mod - <see cref="EntityRegistry.Logger"/> names
/// the assembly so the mistake is not silent.
/// </summary>
public class EntityRegistryTests {
  private sealed class PlainClass;

  public EntityRegistryTests() => EntityRegistry.Logger = null;

  [Fact]
  public void A_domain_fallback_logs_a_warning_naming_the_assembly() {
    var logger = Substitute.For<ILogger>();
    EntityRegistry.Logger = logger;

    // This test assembly declares no [assembly: ExDomain] and is never passed to RegisterAll, so the
    // lookup has nothing but the caller's domain to fall back to.
    string domain = EntityRegistry.DomainOf(
      typeof(PlainClass).Assembly,
      "iiex"
    );

    Assert.Equal("iiex", domain);
    logger.Received(1).Warning(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void No_logger_wired_is_a_silent_no_op() {
    EntityRegistry.Logger = null;
    // Would throw a NullReferenceException if the warning path dereferenced a null Logger.
    string domain = EntityRegistry.DomainOf(
      typeof(PlainClass).Assembly,
      "iiex"
    );
    Assert.Equal("iiex", domain);
  }
}
