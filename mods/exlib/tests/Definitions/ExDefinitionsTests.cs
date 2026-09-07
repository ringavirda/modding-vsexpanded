using System.Reflection;
using ExpandedLib.Definitions;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The re-registration notification: a location registered twice from the same assembly is a mod
/// simply reloading its own defs (silent), while a location registered from two different assemblies
/// is two providers claiming the same asset path (<see cref="ExDefinitions.Logger"/> Notification).
/// </summary>
[Collection("ExDefinitions")]
public class ExDefinitionsTests {
  public ExDefinitionsTests() {
    ExDefinitions.Clear();
    ExDefinitions.Logger = null;
  }

  [Fact]
  public void Re_registering_from_the_same_assembly_logs_nothing() {
    var logger = Substitute.For<ILogger>();
    ExDefinitions.Logger = logger;
    Assembly asm = Assembly.GetExecutingAssembly();

    ExDefinitions.RegisterBlock(ExBlockDef.Create("d", "c"), asm);
    ExDefinitions.RegisterBlock(
      ExBlockDef.Create("d", "c").Resistance(9f),
      asm
    );

    logger.DidNotReceive().Notification(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void Re_registering_from_a_different_assembly_logs_a_notification() {
    var logger = Substitute.For<ILogger>();
    ExDefinitions.Logger = logger;

    ExDefinitions.RegisterBlock(
      ExBlockDef.Create("d", "c"),
      typeof(ExDefinitionsTests).Assembly
    );
    ExDefinitions.RegisterBlock(
      ExBlockDef.Create("d", "c"),
      typeof(ExDefinitions).Assembly
    );

    logger.Received(1).Notification(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void Re_registering_an_item_from_a_different_assembly_logs_a_notification() {
    var logger = Substitute.For<ILogger>();
    ExDefinitions.Logger = logger;

    ExDefinitions.RegisterItem(
      ExItemDef.Create("d", "c"),
      typeof(ExDefinitionsTests).Assembly
    );
    ExDefinitions.RegisterItem(
      ExItemDef.Create("d", "c"),
      typeof(ExDefinitions).Assembly
    );

    logger.Received(1).Notification(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void No_logger_wired_is_a_silent_no_op() {
    ExDefinitions.Logger = null;
    // Would throw a NullReferenceException if the notification path dereferenced a null Logger.
    ExDefinitions.RegisterBlock(
      ExBlockDef.Create("d", "c"),
      typeof(ExDefinitionsTests).Assembly
    );
    ExDefinitions.RegisterBlock(
      ExBlockDef.Create("d", "c"),
      typeof(ExDefinitions).Assembly
    );
  }
}
