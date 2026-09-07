using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ExpandedLib.Testing;

/// <summary>
/// Real files under a throwaway temp directory, backing <c>ICoreAPI.LoadModConfig</c> and
/// <c>StoreModConfig</c> so a config class saved through the real API round-trips through real file
/// I/O rather than a hand-wired substitute. <see cref="TestWorld"/> owns the directory's lifetime -
/// it is deleted on <see cref="TestWorld.Dispose"/> - and routes both generic and JSON-object
/// overloads here through a custom NSubstitute call handler (open generic methods cannot be matched
/// through the ordinary <c>Arg.Any&lt;T&gt;()</c>/<c>Returns</c> pair, since NSubstitute binds a
/// return-value specification to the exact closed generic method it was written against).
/// </summary>
public sealed class ModConfigFiles : IDisposable {
  /// <summary>The directory every file below is written to and read from.</summary>
  public string Directory { get; } =
    Path.Combine(
      Path.GetTempPath(),
      "exlib-testconfig-" + Guid.NewGuid().ToString("N")
    );

  /// <summary>The file names currently on disk, for a test asserting nothing (or something specific)
  /// was written.</summary>
  public IReadOnlyList<string> Files =>
    System.IO.Directory.Exists(Directory)
      ? [.. System.IO.Directory.GetFiles(Directory).Select(Path.GetFileName)!]
      : [];

  /// <summary>Serialises <paramref name="value"/> as indented JSON to <paramref name="file"/>,
  /// creating the directory on first write. <c>null</c> writes the literal JSON <c>null</c>, which
  /// is what storing an unwrapped, empty <c>JsonObject</c> means.</summary>
  public void Write(string file, object? value) {
    System.IO.Directory.CreateDirectory(Directory);
    File.WriteAllText(
      Path.Combine(Directory, file),
      JsonConvert.SerializeObject(value, Formatting.Indented)
    );
  }

  /// <summary>Deserialises <paramref name="file"/> as <typeparamref name="T"/>, or the default value
  /// when the file has never been written.</summary>
  public T? Read<T>(string file) {
    string? json = ReadRaw(file);
    return json == null ? default : JsonConvert.DeserializeObject<T>(json);
  }

  /// <summary>As <see cref="Read{T}"/>, but for the type-erased case the call handler needs: it only
  /// learns <paramref name="type"/> at runtime, off the intercepted method's generic argument.</summary>
  internal object? ReadUntyped(string file, Type type) {
    string? json = ReadRaw(file);
    return json == null ? null : JsonConvert.DeserializeObject(json, type);
  }

  private string? ReadRaw(string file) {
    string path = Path.Combine(Directory, file);
    return File.Exists(path) ? File.ReadAllText(path) : null;
  }

  /// <summary>Deletes the temp directory, best-effort.</summary>
  public void Dispose() {
    try {
      System.IO.Directory.Delete(Directory, recursive: true);
    } catch {
      // Best-effort cleanup; a locked or already-removed directory is not worth failing a test over.
    }
  }
}
