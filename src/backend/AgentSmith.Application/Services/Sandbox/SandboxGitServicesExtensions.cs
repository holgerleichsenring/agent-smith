using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The framework-owned git facts of a repo sandbox: the committing identity (p0411), the
/// working tree's changed paths, the base branch the clone points at and the work-branch
/// checkout that keeps a reused branch level with that base (p0496).
/// </summary>
public static class SandboxGitServicesExtensions
{
    public static IServiceCollection AddSandboxGitServices(this IServiceCollection services)
    {
        services.AddTransient<SandboxGitIdentity>();
        services.AddTransient<SandboxWorkingTreeReader>();
        services.AddTransient<SandboxBaseBranch>();
        services.AddTransient<WorkBranchBaseMerger>();
        services.AddTransient<SandboxWorkBranchCheckout>();
        return services;
    }
}
