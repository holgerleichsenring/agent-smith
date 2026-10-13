using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: the context a baseline is taken for — its meta files, its workdir, the
/// other components of the same repository, whose files are theirs, and what the transfer did
/// with principles.md. 2026-10-13-2d0c: the old Limits a refresh compares with are the baseline's
/// own record, no longer the principles text the round started from.
/// 2026-10-13-50c7: the siblings are claims — workdir plus the sources an earlier round recorded
/// in the pipeline; <see cref="Sources"/> are the round's own, read from the context.yaml it just
/// wrote; <see cref="WorkdirBefore"/> is the workdir of the context.yaml the round started from.
/// Built by <see cref="StructureBaselineScopes"/>.
/// </summary>
public sealed record StructureBaselineScope(
    string ContextName, string Workdir, IReadOnlyList<StructureClaim> Siblings, PrinciplesMode Mode)
{
    public IReadOnlyList<string> Sources { get; init; } = [];

    public string? WorkdirBefore { get; init; }

    public string PrinciplesPath => $"{ProjectMetaPaths.MetaDirFor(ContextName)}/{ProjectMetaPaths.PrinciplesFile}";

    public string BaselinePath => $"{ProjectMetaPaths.MetaDirFor(ContextName)}/{StructureBaselineFile.FileName}";
}
