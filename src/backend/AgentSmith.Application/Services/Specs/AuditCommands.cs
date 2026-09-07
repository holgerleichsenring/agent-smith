using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one ecosystem's own advisory command, as a fixed argument vector.
/// <para>
/// The security scan runs the same commands through <c>/bin/sh -c</c> and installs
/// pip-audit on a miss; here nothing is a shell line and nothing is installed — a
/// derivation that finds the tool absent is told so and states an assumption. The
/// pip-audit form reads requirements.txt when that is the marker, exactly as the scan
/// does; the .NET form audits DIRECT references only, which the tool's description says.
/// </para>
/// </summary>
internal static class AuditCommands
{
    private const int TimeoutSeconds = 180;
    private const string RequirementsMarker = "requirements.txt";

    /// <summary>Null for an ecosystem that has no advisory command here (Go).</summary>
    public static Step? For(PackageEcosystem ecosystem) => ecosystem.Kind switch
    {
        PackageEcosystemKind.DotNet =>
            Run("dotnet", ["list", "package", "--vulnerable", "--format", "json"]),
        PackageEcosystemKind.Npm => Run("npm", ["audit", "--json"]),
        PackageEcosystemKind.Python => Run("pip-audit",
            ecosystem.Marker == RequirementsMarker
                ? ["--format=json", $"--requirement={RequirementsMarker}"]
                : ["--format=json"]),
        _ => null,
    };

    /// <summary>What the tool ran, in the words its evidence line carries.</summary>
    public static string Describe(Step step) =>
        $"{step.Command} {string.Join(' ', step.Args ?? [])}";

    /// <summary>Whether an exit code means the tool reached a verdict. Every one of the
    /// three exits 0 for "nothing found" and 1 for "findings"; anything else is the tool
    /// failing to run, which proves nothing about the dependencies.</summary>
    public static bool ReachedAVerdict(int exitCode) => exitCode is 0 or 1;

    private static Step Run(string command, string[] args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: command, Args: args, WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: TimeoutSeconds);
}
