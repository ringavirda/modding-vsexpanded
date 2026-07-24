// On the legacy target frameworks, bring exlib's LegacyApi/LegacyAnimUtil extension members
// into scope across the assembly so the mod code keeps using the 1.22 API shapes unchanged.
// Absent on the current version, where the real game members are used.
#if !GAME_GE_1_22
global using ExpandedLib.Legacy;
#endif
