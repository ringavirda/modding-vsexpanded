using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shared list/show/set scaffold <see cref="ConfigSubCommand"/> and <see cref="RecipesSubCommand"/>
/// derive from, exercised through a tiny in-memory registry of its own rather than either mod command's
/// real one.
/// </summary>
public class RegistrySubCommandTests {
  private sealed class Widget {
    public required string Code;
    public string Value = "default";
  }

  private sealed class WidgetCommand : RegistrySubCommand<Widget> {
    private readonly Dictionary<string, Widget> _widgets;

    public WidgetCommand(Dictionary<string, Widget> widgets)
      : base(
        "widget",
        "exlib:command-config-desc", // any existing key; only its resolution is exercised elsewhere
        () => widgets.Keys,
        code => widgets.TryGetValue(code, out var w) ? w : null
      ) => _widgets = widgets;

    protected override string NoneRegisteredKey => "exlib:command-config-none";
    protected override string ListHeaderKey => "exlib:command-config-list";
    protected override string UnknownCodeKey => "exlib:command-config-unknown";

    protected override string Describe(Widget entry) =>
      $"{entry.Code}={entry.Value}";

    // Mirrors ConfigSubCommand's two-level shape: `<field> <value>` to write, `<field>` alone is
    // missing its value, no words at all shows the current one.
    protected override TextCommandResult Set(Widget entry, string[] args) {
      if (args.Length == 0)
        return TextCommandResult.Success($"current: {entry.Value}");
      if (args.Length == 1)
        return TextCommandResult.Error($"missing a value for '{args[0]}'");

      entry.Value = args[1];
      return TextCommandResult.Success($"set to {entry.Value}");
    }
  }

  private static WidgetCommand Command(params Widget[] widgets) {
    var byCode = new Dictionary<string, Widget>();
    foreach (Widget w in widgets)
      byCode[w.Code] = w;
    return new WidgetCommand(byCode);
  }

  [Fact]
  public void No_code_lists_every_entry_via_Describe() {
    var cmd = Command(
      new Widget { Code = "a", Value = "1" },
      new Widget { Code = "b", Value = "2" }
    );

    TextCommandResult result = cmd.Dispatch(null, null);

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Contains("a=1", result.StatusMessage);
    Assert.Contains("b=2", result.StatusMessage);
  }

  [Fact]
  public void A_code_with_no_further_words_calls_Set_with_no_args() {
    var cmd = Command(new Widget { Code = "a", Value = "1" });

    TextCommandResult result = cmd.Dispatch("a", null);

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Equal("current: 1", result.StatusMessage);
  }

  [Fact]
  public void A_code_with_trailing_words_hands_all_of_them_to_Set() {
    var widget = new Widget { Code = "a", Value = "1" };
    var cmd = Command(widget);

    TextCommandResult result = cmd.Dispatch("a", "field 2");

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Equal("2", widget.Value);
    Assert.Equal("set to 2", result.StatusMessage);
  }

  [Fact]
  public void An_unknown_code_errors_without_calling_Set() {
    var cmd = Command(new Widget { Code = "a", Value = "1" });

    TextCommandResult result = cmd.Dispatch("nope", "2");

    Assert.Equal(EnumCommandStatus.Error, result.Status);
  }

  [Fact]
  public void A_field_with_no_value_reports_missing_arguments() {
    // The base only decides "show" (no words) versus "hand these words to Set"; how many Set then
    // needs is the derived command's own business, same as ConfigSubCommand requiring a newvalue.
    var cmd = Command(new Widget { Code = "a", Value = "1" });

    TextCommandResult result = cmd.Dispatch("a", "field");

    Assert.Equal(EnumCommandStatus.Error, result.Status);
    Assert.Equal("missing a value for 'field'", result.StatusMessage);
  }

  [Fact]
  public void A_bare_code_with_only_whitespace_after_it_is_treated_as_show() {
    var cmd = Command(new Widget { Code = "a", Value = "1" });

    TextCommandResult result = cmd.Dispatch("a", "   ");

    Assert.Equal(EnumCommandStatus.Success, result.Status);
    Assert.Equal("current: 1", result.StatusMessage);
  }
}
