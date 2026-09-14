using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The framework-owned git facts of a repo sandbox: the committing identity (p0411), the
/// working tree's changed paths, the base branch the clone points at, the ladder that
/// resolves ONE base per repository from it (2026-09-13-5cdf), the create-only publisher
/// that puts a feature's branch on the remote once (2026-09-13-35a4), where the run began
/// (2026-09-01-b467) and the work-branch checkout that cuts from that base and keeps a
/// reused branch level with it (p0496).
/// </summary>
public static class SandboxGitServicesExtensions
{
    public static IServiceCollection AddSandboxGitServices(this IServiceCollection services)
    {
        services.AddTransient<SandboxGitIdentity>();
        services.AddTransient<SandboxWorkingTreeReader>();
        services.AddTransient<SandboxBaseBranch>();
        services.AddTransient<SandboxBaseLadder>();
        services.AddTransient<SandboxRunStartCommit>();
        services.AddTransient<CreateOnlyBranchPush>();
        services.AddTransient<SandboxRungPublisher>();
        services.AddTransient<WorkBranchBaseMerger>();
        services.AddTransient<WorkBranchBaseMergeReport>();
        services.AddTransient<SandboxWorkBranchCheckout>();
        return services;
    }
}
