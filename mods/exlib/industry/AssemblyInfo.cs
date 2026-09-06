using ExpandedLib.Registries;

// Same domain as exlib's own AssemblyInfo.cs, for the same reason: this assembly ships inside the
// exlib mod folder, under the exlib asset domain, and a class name resolved through
// ExBlockDef.Class<T>() before the mod's Start has run must still come back "exlib.Xxx" and
// not fall back to whichever mod happened to be asking.
[assembly: ExDomain("exlib")]

// A framework module, hosted by exlib itself - the default Host on ExModuleAttribute.
[assembly: ExModule("industry")]
