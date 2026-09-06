using ExpandedLib.Registries;

// Same domain as exlib's own AssemblyInfo.cs, for the same reason: this assembly ships inside the
// exlib mod folder, under the exlib asset domain, and a class name resolved through
// ExBlockDef.Class<T>() before the mod's Start has run must still come back "exlib.Xxx" and
// not fall back to whichever mod happened to be asking.
[assembly: ExDomain("exlib")]

// A framework module, hosted by exlib itself - the default Host on ExModuleAttribute. Ships inside
// exlib's own mod folder rather than as its own mod, so its Mod (the id ExModules.For checks for
// "enabled") is exlib's, not "industry".
[assembly: ExModule("industry", Mod = "exlib")]
