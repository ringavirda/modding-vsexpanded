using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ExpandedLib.Verify.Tests;

/// <summary>
/// One fact per fixture under <c>Fixtures/</c>, each asserting the finding set and exit code a
/// specific defect (or a clean mod) produces. Every fixture is a minimal, otherwise-clean mod that
/// differs from <c>clean/</c> in exactly the one way its name says.
/// </summary>
public class RunnerTests {
  private static (int ExitCode, List<Finding> Findings) Verify(string fixture, params string[] extraArgs) {
    string[] args = [FixturePath.Of(fixture), .. extraArgs];
    int exit = Runner.Run(args, TextWriter.Null, TextWriter.Null, out List<Finding> findings);
    return (exit, findings);
  }

  [Fact]
  public void Clean_mod_has_no_findings_and_exits_zero() {
    (int exit, List<Finding> findings) = Verify("clean");
    Assert.Empty(findings);
    Assert.Equal(0, exit);
  }

  [Fact]
  public void Bad_json_reports_a_parse_error_with_line_and_column() {
    (int exit, List<Finding> findings) = Verify("bad-json");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Error, finding.Level);
    Assert.Equal("JsonParse", finding.Check);
    Assert.NotNull(finding.Line);
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Bad_patch_target_reports_the_missing_file() {
    (int exit, List<Finding> findings) = Verify("bad-patch-target");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Error, finding.Level);
    Assert.Equal("PatchTarget", finding.Check);
    Assert.Contains("nosuchitem.json", finding.Message);
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Bad_patch_op_reports_the_path_that_does_not_apply() {
    (int exit, List<Finding> findings) = Verify("bad-patch-op");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Error, finding.Level);
    Assert.Equal("PatchOp", finding.Check);
    Assert.Contains("noSuchProperty", finding.Message);
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Bad_handbook_lang_reports_the_unresolved_key() {
    (int exit, List<Finding> findings) = Verify("bad-handbook-lang");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Error, finding.Level);
    Assert.Equal("HandbookLang", finding.Check);
    Assert.Contains("handbook-text-missing", finding.Message);
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Bad_recipe_code_reports_the_unresolved_output() {
    (int exit, List<Finding> findings) = Verify("bad-recipe-code");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Error, finding.Level);
    Assert.Equal("RecipeCode", finding.Check);
    Assert.Contains("nosuchoutput", finding.Message);
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Soft_depends_is_informational_only_and_exits_zero() {
    (int exit, List<Finding> findings) = Verify("soft-depends");
    Finding finding = Assert.Single(findings);
    Assert.Equal(FindingLevel.Info, finding.Level);
    Assert.Equal("PatchDependsOn", finding.Check);
    Assert.Contains("somemissingmod", finding.Message);
    Assert.Equal(0, exit);
  }

  [Fact]
  public void Strict_fails_the_run_on_an_informational_finding_alone() {
    (int exit, _) = Verify("soft-depends", "--strict");
    Assert.Equal(1, exit);
  }

  [Fact]
  public void Json_output_is_a_stable_array_shape() {
    string[] args = [FixturePath.Of("bad-recipe-code"), "--json"];
    var output = new StringWriter();
    Runner.Run(args, output, TextWriter.Null, out _);
    string text = output.ToString();
    Assert.Contains("\"level\": \"error\"", text);
    Assert.Contains("\"check\": \"RecipeCode\"", text);
  }
}
