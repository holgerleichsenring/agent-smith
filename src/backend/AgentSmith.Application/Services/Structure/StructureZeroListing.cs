using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-2d0c: reads a NUL-separated git listing as the sandbox captured it. The agent
/// captures stdout line by line and stops at <see cref="CaptureCap"/>, appending a newline to
/// every line it keeps — so a listing that is ONE line was kept whole at any length, while one
/// of several lines that reached the cap may have lost its tail and is refused (null).
/// </summary>
public sealed class StructureZeroListing
{
    public const int CaptureCap = SizeLimits.RunStepCapturedStdoutMaxChars;

    public IReadOnlyList<string>? Parse(string? output)
    {
        if (output is null) return null;
        var raw = output.TrimEnd('\n');
        if (raw.Contains('\n') && output.Length >= CaptureCap) return null;
        return raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
