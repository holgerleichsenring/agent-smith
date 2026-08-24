using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0504: turns a context's declared <c>meta.workdir</c> into the absolute path it
/// occupies inside its sandbox. Extracted from VerifyPhaseHandler so the verify gate
/// and the domain-profile stages agree on where a context's commands run — the same
/// sub-tree, computed once.
/// </summary>
public static class SandboxWorkdir
{
    /// <summary>Absolute in-sandbox path for a declared (possibly null) workdir.</summary>
    public static string Resolve(string? workdir)
    {
        var normalized = Normalize(workdir);
        return normalized == "." ? Repository.SandboxWorkPath
            : $"{Repository.SandboxWorkPath}/{normalized}";
    }

    private static string Normalize(string? workdir)
    {
        if (string.IsNullOrWhiteSpace(workdir)) return ".";
        var trimmed = workdir.Trim().Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "." : trimmed;
    }
}
