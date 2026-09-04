using IronIndustryExpanded.BlockStructures.Casting;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The pattern <c>mold</c> attribute contract: a well-formed spec parses to its fields, and every
/// malformed field is rejected at load time with a reason. Pure parsing, no world involved.
/// </summary>
public class MoldSpecTests {
  private static JsonObject Obj(string json) => new(JToken.Parse(json));

  private const string Valid = """
    {
      "size": "cell",
      "shape": "iiex:casting/cell-filling-plate",
      "capacity": 136,
      "cavity": [ { "x1": 3, "y1": 12, "z1": 3, "x2": 13, "y2": 14, "z2": 13 } ],
      "output": { "type": "block", "code": "iiex:casting-mold-ingot" },
      "minPourTemp": 1150
    }
    """;

  [Fact]
  public void A_valid_spec_parses_to_its_fields() {
    Assert.True(
      MoldSpec.TryParse(Obj(Valid), out MoldSpec? spec, out string? error)
    );
    Assert.Null(error);
    Assert.NotNull(spec);
    Assert.Equal(MoldSize.Cell, spec!.Size);
    Assert.Equal("iiex:casting/cell-filling-plate", spec.Shape);
    Assert.Equal(136, spec.Capacity);
    Assert.Single(spec.Cavity);
    Assert.Equal(1150f, spec.MinPourTemp);
    Assert.Equal("iiex:casting-mold-ingot", spec.Output.Code.ToString());
  }

  [Fact]
  public void A_pattern_written_before_the_field_existed_reads_as_the_first_schema() {
    // Every shipped pattern is this form. Refusing it, or reading it as anything but schema 1, would
    // break the whole casting catalogue the day the field was added.
    Assert.True(
      MoldSpec.TryParse(Obj(Valid), out MoldSpec? spec, out string? error),
      error
    );
    Assert.Equal(1, spec!.Schema);
  }

  [Fact]
  public void A_pattern_from_a_newer_build_is_refused_rather_than_mis_read() {
    Assert.False(
      MoldSpec.TryParse(
        Obj(
          Valid.Replace(
            "\"size\": \"cell\"",
            "\"schema\": 99, \"size\": \"cell\""
          )
        ),
        out _,
        out string? error
      )
    );
    Assert.Contains("99", error!);
  }

  [Fact]
  public void Longcell_size_parses() {
    Assert.True(
      MoldSpec.TryParse(
        Obj(Valid.Replace("\"cell\"", "\"longcell\"")),
        out MoldSpec? spec,
        out _
      )
    );
    Assert.Equal(MoldSize.LongCell, spec!.Size);
  }

  [Fact]
  public void MinPourTemp_defaults_to_zero_when_absent() {
    const string noTemp = """
      {
        "size": "cell",
        "shape": "iiex:casting/cell-filling-plate",
        "capacity": 136,
        "cavity": [ { "x1": 3, "y1": 12, "z1": 3, "x2": 13, "y2": 14, "z2": 13 } ],
        "output": { "type": "block", "code": "iiex:casting-mold-ingot" }
      }
      """;
    Assert.True(MoldSpec.TryParse(Obj(noTemp), out MoldSpec? spec, out _));
    Assert.Equal(0f, spec!.MinPourTemp);
  }

  [Theory]
  [InlineData("\"size\": \"cell\"", "\"size\": \"barrelrack\"")] // bad size
  [InlineData("\"capacity\": 136", "\"capacity\": 0")] // non-positive capacity
  [InlineData(
    "\"shape\": \"iiex:casting/cell-filling-plate\"",
    "\"shape\": \"\""
  )] // empty shape
  [InlineData(
    "\"cavity\": [ { \"x1\": 3, \"y1\": 12, \"z1\": 3, \"x2\": 13, \"y2\": 14, \"z2\": 13 } ]",
    "\"cavity\": []"
  )] // no cavity boxes
  [InlineData(
    "\"x2\": 13, \"y2\": 14, \"z2\": 13",
    "\"x2\": 3, \"y2\": 14, \"z2\": 13"
  )] // inverted box (x2 <= x1)
  public void A_malformed_field_is_rejected_with_a_reason(
    string from,
    string to
  ) {
    Assert.False(
      MoldSpec.TryParse(
        Obj(Valid.Replace(from, to)),
        out MoldSpec? spec,
        out string? error
      )
    );
    Assert.Null(spec);
    Assert.False(string.IsNullOrWhiteSpace(error));
  }

  [Fact]
  public void A_missing_output_is_rejected() {
    string noOut = Valid.Replace(
      "\"output\": { \"type\": \"block\", \"code\": \"iiex:casting-mold-ingot\" },",
      ""
    );
    // The cut has to land, or the spec stays valid and the rejection below passes for the wrong
    // reason. Matching the line terminator too would tie the cut to the checkout's line endings.
    Assert.NotEqual(Valid, noOut);
    Assert.False(MoldSpec.TryParse(Obj(noOut), out _, out string? error));
    Assert.Contains("output", error);
  }

  [Fact]
  public void A_null_or_absent_attribute_is_rejected() {
    Assert.False(MoldSpec.TryParse(null, out _, out string? error));
    Assert.False(string.IsNullOrWhiteSpace(error));
  }
}
