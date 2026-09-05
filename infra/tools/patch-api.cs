// Publicizes interface members that Vintage Story ships as `internal abstract`.
//
// An interface member is a vtable slot every implementer must fill, but an `internal` one cannot be
// filled from another assembly: the compiler demands it (CS0535) and forbids it (CS0122) in the same
// build, and the CLR enforces the same rule on override. The declaring interface therefore cannot be
// implemented or mocked by any mod or test assembly - Castle DynamicProxy fails at proxy creation
// with "does not have an implementation", which is what takes out every Substitute.For<IPlayer>().
//
// VintagestoryAPI.dll is not strong-named, so flipping the accessibility bits is safe. The edit is
// applied in place, two bits at a time, directly on the MethodDef row's Flags column: the assembly
// keeps its exact length, its metadata layout, and - critically - its CodeView debug directory entry.
//
// That last point is not cosmetic. .game/<slug> is both the build's reference source and the install
// the game is launched from, and LoggerBase's static constructor resolves its own source path through
// StackFrame.GetFileName(). If the assembly is regenerated (as any Mono.Cecil round-trip does), the
// CodeView entry no longer matches VintagestoryAPI.pdb, GetFileName() returns null, and that cctor
// throws before the client can create a single logger - the game dies with no log file written and a
// crash reporter that cannot report, because it needs the same cctor to format the trace. Rewriting
// the assembly is therefore not an option here; only an in-place edit is.
//
// Idempotent, and a no-op once upstream makes the member public, so it can stay in place across
// updates and disappear on its own.
//
//   dotnet run infra/tools/patch-api.cs -- <path to VintagestoryAPI.dll>

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Every non-public overload of these members is made public. Matching on name alone is deliberate:
// an interface proxy must implement ALL overloads, so publicizing only one still leaves the type
// unmockable.
var targets = new (string Type, string Method, string Reason)[] {
  (
    "Vintagestory.API.Common.IPlayer",
    "IsInInteractionRangeOf",
    "VS 1.22.6 - blocks every IPlayer mock and hand-written test double"
  ),
};

if (args.Length < 1) {
  Console.Error.WriteLine("usage: patch-api.cs <path to VintagestoryAPI.dll>");
  return 2;
}

string path = args[0];
if (!File.Exists(path)) {
  Console.Error.WriteLine($"patch-api: not found: {path}");
  return 2;
}

// MethodDef row: RVA (4) | ImplFlags (2) | Flags (2) | Name | Signature | ParamList.
const int FlagsColumnOffset = 6;
const MethodAttributes AccessMask = MethodAttributes.MemberAccessMask;

var edits = new List<(long Offset, ushort From, ushort To, string What)>();
int missing = 0;

// Read pass: locate each target and work out the file offset of its Flags column. The PEReader owns
// the stream while it is open, so nothing is written until it has been disposed.
using (var readStream = File.OpenRead(path))
using (var pe = new PEReader(readStream)) {
  MetadataReader md = pe.GetMetadataReader();
  int metadataStart = pe.PEHeaders.MetadataStartOffset;
  int tableOffset = md.GetTableMetadataOffset(TableIndex.MethodDef);
  int rowSize = md.GetTableRowSize(TableIndex.MethodDef);

  foreach (var (typeName, methodName, reason) in targets) {
    string ns = typeName[..typeName.LastIndexOf('.')];
    string name = typeName[(typeName.LastIndexOf('.') + 1)..];

    TypeDefinition? type = null;
    foreach (TypeDefinitionHandle h in md.TypeDefinitions) {
      TypeDefinition candidate = md.GetTypeDefinition(h);
      if (md.GetString(candidate.Name) == name
        && md.GetString(candidate.Namespace) == ns) {
        type = candidate;
        break;
      }
    }
    if (type is null) {
      Console.WriteLine($"patch-api: {typeName} not present - skipped");
      missing++;
      continue;
    }

    int found = 0;
    foreach (MethodDefinitionHandle h in type.Value.GetMethods()) {
      MethodDefinition method = md.GetMethodDefinition(h);
      if (md.GetString(method.Name) != methodName) continue;
      found++;

      if ((method.Attributes & AccessMask) == MethodAttributes.Public) {
        Console.WriteLine(
          $"patch-api: {typeName}.{methodName} (row {MetadataTokens.GetRowNumber(h)}) already public - no change");
        continue;
      }

      long offset = metadataStart
        + tableOffset
        + (long)(MetadataTokens.GetRowNumber(h) - 1) * rowSize
        + FlagsColumnOffset;
      ushort from = (ushort)method.Attributes;
      ushort to = (ushort)((method.Attributes & ~AccessMask) | MethodAttributes.Public);
      edits.Add((offset, from, to, $"{typeName}.{methodName}  [{reason}]"));
    }

    if (found == 0) {
      // Expected on versions that predate the member, and on versions where it was removed again.
      Console.WriteLine($"patch-api: {typeName}.{methodName} not present - skipped");
      missing++;
    }
  }
}

if (edits.Count == 0) {
  Console.WriteLine($"patch-api: nothing to change ({missing} target(s) absent)");
  return 0;
}

// Write pass. Each offset is verified against the flags the metadata reader reported before anything
// is written, so a miscomputed row offset aborts instead of corrupting the assembly.
using (var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite)) {
  var buffer = new byte[2];
  foreach (var (offset, from, to, what) in edits) {
    fs.Position = offset;
    fs.ReadExactly(buffer);
    ushort actual = (ushort)(buffer[0] | (buffer[1] << 8));
    if (actual != from) {
      Console.Error.WriteLine(
        $"patch-api: refusing to write - expected flags 0x{from:x4} at offset {offset}, found 0x{actual:x4}");
      return 3;
    }

    fs.Position = offset;
    fs.Write([(byte)(to & 0xFF), (byte)(to >> 8)]);
    Console.WriteLine($"patch-api: {what}  flags 0x{from:x4} -> 0x{to:x4}");
  }
}

Console.WriteLine($"patch-api: wrote {edits.Count} change(s) to {path}");
return 0;
