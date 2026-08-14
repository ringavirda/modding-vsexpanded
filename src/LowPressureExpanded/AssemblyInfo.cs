using ExpandedLib.Registries.Entities;

// The domain every registrable type in this assembly is keyed under, and the assets/<domain>/ tree it
// ships as. Declared here rather than derived from the mod id at registration time so a key resolves
// with no load-order dependency: a dependent mod naming one of our classes through
// ExBlockDef.Class<T>() gets "lpex.Xxx" even if our Start has not run yet.
[assembly: ExDomain("lpex")]
