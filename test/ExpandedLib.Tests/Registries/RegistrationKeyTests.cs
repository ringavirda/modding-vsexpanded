using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="EntityRegistry.KeyFor"/> is the single source of the class-string↔type binding, shared by
/// the class registration (<c>RegisterAll</c>) and the code-first builder's type-safe <c>Class&lt;T&gt;()</c>.
/// These pin the exact key it produces for each attribute shape so the two consumers can never desync.
/// </summary>
public class RegistrationKeyTests {
  [BlockRegister]
  private sealed class ConventionBlock : Block { }

  [BlockRegister("customcode")]
  private sealed class CodedBlock : Block { }

  [BlockRegister("Vanilla", PrefixModId = false)]
  private sealed class BareBlock : Block { }

  private sealed class NoAttributeBlock : Block { }

  [Fact]
  public void Default_convention_is_modid_dot_classname() {
    Assert.Equal(
      "iwex.ConventionBlock",
      EntityRegistry.KeyFor("iwex", typeof(ConventionBlock))
    );
  }

  [Fact]
  public void Explicit_code_replaces_the_class_name_but_keeps_the_prefix() {
    Assert.Equal(
      "iwex.customcode",
      EntityRegistry.KeyFor("iwex", typeof(CodedBlock))
    );
  }

  [Fact]
  public void PrefixModId_false_registers_under_a_bare_key() {
    // As used when replacing a vanilla class - no {modid}. prefix.
    Assert.Equal("Vanilla", EntityRegistry.KeyFor("iwex", typeof(BareBlock)));
  }

  [Fact]
  public void A_type_without_a_register_attribute_falls_back_to_the_convention() {
    Assert.Equal(
      "iwex.NoAttributeBlock",
      EntityRegistry.KeyFor("iwex", typeof(NoAttributeBlock))
    );
  }
}
