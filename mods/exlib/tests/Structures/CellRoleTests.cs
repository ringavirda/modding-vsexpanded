using System;
using ExpandedLib.Structures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="CellRole"/> is an open string key: exlib declares none, and any mod may mint one with
/// <see cref="CellRole.Of"/>.
/// </summary>
public class CellRoleTests {
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void Of_rejects_blank(string? key) =>
    Assert.Throws<ArgumentException>(() => CellRole.Of(key!));

  [Fact]
  public void Two_roles_with_one_key_are_equal() =>
    Assert.Equal(CellRole.Of("tuyere"), CellRole.Of("tuyere"));
}
