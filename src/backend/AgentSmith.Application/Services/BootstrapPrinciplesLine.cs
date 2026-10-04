using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// The sentence the bootstrap prompt uses to tell the skill what the framework already did to
/// principles.md in transfer, refresh and preserve mode — the skill leaves the file as it is in
/// all three. Its own type because <see cref="BootstrapPromptFactory"/> sits at its file-length
/// baseline (2026-10-04-2bf2 added the refresh sentence).
/// </summary>
internal static class BootstrapPrinciplesLine
{
    public static string For(PrinciplesMode mode, string principlesPath) => mode switch
    {
        PrinciplesMode.Transferred =>
            $"`{principlesPath}` is already in place — transferred from the "
            + "authored universal core plus this component's language delta.",
        PrinciplesMode.Refreshed =>
            $"`{principlesPath}` was refreshed — recomposed from the authored universal core, "
            + "this component's language delta and its framework overlays, with any existing "
            + "Project Specifics section carried over.",
        _ => $"`{principlesPath}` already exists and is preserved as ratified.",
    };
}
