// On the legacy target frameworks, brings exlib's LegacyApi extension members into scope so the tests
// keep using the 1.22 API shapes (e.g. CraftingRecipeIngredient.ResolvedItemStack). On the current
// version the real game members are used instead.
#if !GAME_GE_1_22
global using ExpandedLib.Legacy;
#endif
