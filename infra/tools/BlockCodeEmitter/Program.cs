using System;
using System.IO;
using System.Reflection;
using ExpandedLib.Testing;

// Regenerates one mod's `{Mod}Blocks.g.cs` code-code table straight from its definitions, replacing
// the EXLIB_WRITE_BLOCKCODES env-var switch the test run used to expose. Invoked through
// `scripts/exmod.ps1 codes <mod>` rather than run directly, so the mod is built first and the
// written file is rebuilt afterward to catch a change that no longer compiles.
//
// usage: BlockCodeEmitter <mod>   (mod is exlib, iiex or siex)

if (args.Length != 1) {
  Console.Error.WriteLine(
    "usage: BlockCodeEmitter <mod>  (mod is exlib, iiex or siex)"
  );
  return 2;
}

VsAssemblyResolver.Register();

string mod = args[0];
(
  string Domain,
  Assembly Asm,
  string ClassName,
  string Namespace,
  string OutPath
) target = mod switch {
  // One already-loaded type per mod, the same anchor each mod's own *BlocksCodeTests fixture uses.
  "exlib" => (
    "exlib",
    typeof(ExpandedLib.Structures.StructureFillers).Assembly,
    "ExlibBlocks",
    "ExpandedLib",
    "mods/exlib/src/Generated/ExlibBlocks.g.cs"
  ),
  "iiex" => (
    "iiex",
    typeof(IronIndustryExpanded.Recipes.Grid.FurnaceRecipeDefinitions).Assembly,
    "IiexBlocks",
    "IronIndustryExpanded",
    "mods/iiex/src/Generated/IiexBlocks.g.cs"
  ),
  "siex" => (
    "siex",
    typeof(SteelIndustryExpanded.BlockStructures.SmokeStack.Blocks.BlockSmokeStackIntake).Assembly,
    "SiexBlocks",
    "SteelIndustryExpanded",
    "mods/siex/src/Generated/SiexBlocks.g.cs"
  ),
  _ => throw new ArgumentException(
    $"unknown mod '{mod}' - expected exlib, iiex or siex"
  ),
};

string text = BlockCodeEmitter.Emit(
  target.Domain,
  target.Asm,
  target.ClassName,
  target.Namespace
);
string fullPath = Path.Combine(
  DefinitionGoldens.RepoRoot(),
  target.OutPath.Replace('/', Path.DirectorySeparatorChar)
);
File.WriteAllText(fullPath, text);
Console.WriteLine($"wrote {target.OutPath}");
return 0;
