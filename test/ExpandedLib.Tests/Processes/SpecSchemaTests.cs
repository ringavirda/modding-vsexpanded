using ExpandedLib.Processes;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The versioning contract every spec attribute carries. A parser must read every form that has shipped,
/// and must refuse one it cannot know the shape of rather than mis-reading it as the form it does know.
/// See docs/design/mechanics/process-extension.md.
/// </summary>
public class SpecSchemaTests {
  private static JsonObject Json(string json) => new(JToken.Parse(json));

  private const int Current = 2;

  private static int Read(string json, int current = Current) {
    Assert.True(
      SpecSchema.TryRead(
        Json(json),
        current,
        out int schema,
        out string? error
      ),
      error
    );
    return schema;
  }

  private static string Rejects(string json, int current = Current) {
    Assert.False(
      SpecSchema.TryRead(Json(json), current, out _, out string? error)
    );
    Assert.NotNull(error);
    return error!;
  }

  [Fact]
  public void A_declaration_with_no_schema_reads_as_the_first_one() {
    // Absent is not the same as unversioned: the first form is the one that shipped before the field
    // existed, so it has a number whether or not it was written down.
    Assert.Equal(SpecSchema.First, Read("""{ "family": "shingledbar" }"""));
    Assert.Equal(1, SpecSchema.First);
  }

  [Fact]
  public void A_declared_schema_is_read_back() {
    Assert.Equal(2, Read("""{ "schema": 2 }"""));
  }

  [Fact]
  public void An_older_form_is_still_read() {
    // The whole point of the number: a spec written against schema 1 keeps loading on a build that has
    // moved to 2. Refusing it would break someone's content on our schedule.
    Assert.Equal(1, Read("""{ "schema": 1 }""", current: 2));
  }

  [Fact]
  public void A_schema_from_a_newer_build_is_refused_by_name() {
    // We cannot know what changed, so reading it as the form we do know would mis-parse it silently. The
    // error names both numbers, because the fix is on the reader's side - update the library.
    string error = Rejects("""{ "schema": 3 }""", current: 2);

    Assert.Contains("3", error);
    Assert.Contains("2", error);
  }

  [Fact]
  public void A_schema_below_the_first_one_is_malformed() {
    Assert.Contains("schema", Rejects("""{ "schema": 0 }"""));
    Assert.Contains("schema", Rejects("""{ "schema": -1 }"""));
  }

  [Fact]
  public void A_missing_node_reads_as_the_first_form_rather_than_throwing() {
    Assert.True(SpecSchema.TryRead(null, Current, out int schema, out _));
    Assert.Equal(SpecSchema.First, schema);
  }
}
