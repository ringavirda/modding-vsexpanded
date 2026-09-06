using System;
using ExpandedLib.Verify;

// exlib-verify <modpath> [--game <install>] [--mods <dir>...] [--json] [--strict]
//
// Checks a JSON-only Vintage Story mod without the game running: does every asset parse, does
// every patch apply against its real target, does every recipe/handbook code resolve. See
// mods/exlib/wiki/Checks.md, "Without the game: exlib-verify", for the full error/informational
// split this drives. The actual work lives in Runner.Run so ExlibVerify.Tests can drive it
// in-process against a fixture folder.
return Runner.Run(args, Console.Out, Console.Error);
