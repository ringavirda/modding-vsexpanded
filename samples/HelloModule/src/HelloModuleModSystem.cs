using Vintagestory.API.Common;

namespace HelloModule;

/// <summary>
/// The game's Code-mod loader refuses a dll with no <see cref="ModSystem"/> and no
/// <c>ModDependency</c> attribute at all ("no .dll files that contain at least one ModSystem or has
/// a ModInfo attribute"), so this empty placeholder exists purely to satisfy that check - exlib's own
/// <c>ExModuleModSystem</c> is what actually drives <see cref="HelloModule"/>.
/// </summary>
public class HelloModuleModSystem : ModSystem { }
