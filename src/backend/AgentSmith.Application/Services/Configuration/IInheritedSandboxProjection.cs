using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-09-22-6968: what each project WOULD inherit if it declared no sandbox block. The
/// answer nothing in the product computed before, and the only one a null-means-inherit
/// control can honestly show as its placeholder.
/// </summary>
public interface IInheritedSandboxProjection
{
    /// <summary>What a project that declares nothing at all inherits — the process-wide
    /// values. This is also the answer for a DRAFT project: the projection is keyed by the
    /// name of a project the running configuration holds, so one being created has no row
    /// of its own, and these are exactly what it will inherit once it exists.</summary>
    InheritedSandboxSettings ProcessWide();

    /// <summary>One row per configured project, keyed by project name.</summary>
    IReadOnlyDictionary<string, InheritedSandboxSettings> ByProject();
}
