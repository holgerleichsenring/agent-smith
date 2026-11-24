using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>2026-11-23-933dd: one build of a committed revision — the run's context, the revision as last read, the
/// PoC's target and scan block as configured now, the source repo whose host builds, and the commit it builds.</summary>
public sealed record PocBuildJob(
    PocPipelineBuildContext Context, PocScanSubject Subject, PocTargetConfig Target, PocScanConfig Scan, RepoConnection Repo,
    string Sha)
{
    public PocBuildConfig Build => Target.Build;
}
