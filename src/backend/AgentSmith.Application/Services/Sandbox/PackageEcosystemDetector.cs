using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Marker-file detection in the order the security scan has always applied it: a
/// package manifest wins over a Python manifest, which wins over a project file found
/// anywhere in the tree, which wins over a Go module. The order is kept exactly so the
/// scan's verdicts do not move when the detection is shared.
/// </summary>
public sealed class PackageEcosystemDetector : IPackageEcosystemDetector
{
    private const int ProjectFileSearchDepth = 8;
    private const string ProjectFileExtension = ".csproj";

    private static readonly (PackageEcosystemKind Kind, string Marker)[] RootMarkers =
    [
        (PackageEcosystemKind.Npm, "package-lock.json"),
        (PackageEcosystemKind.Npm, "package.json"),
        (PackageEcosystemKind.Python, "requirements.txt"),
        (PackageEcosystemKind.Python, "pyproject.toml"),
    ];

    public async Task<PackageEcosystem?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        foreach (var (kind, marker) in RootMarkers)
            if (await reader.ExistsAsync(Path.Combine(repoPath, marker), cancellationToken))
                return new PackageEcosystem(kind, marker);

        var entries = await reader.ListAsync(repoPath, ProjectFileSearchDepth, cancellationToken);
        var project = entries.FirstOrDefault(
            e => e.EndsWith(ProjectFileExtension, StringComparison.OrdinalIgnoreCase));
        if (project is not null)
            return new PackageEcosystem(PackageEcosystemKind.DotNet, project);

        const string goModule = "go.mod";
        return await reader.ExistsAsync(Path.Combine(repoPath, goModule), cancellationToken)
            ? new PackageEcosystem(PackageEcosystemKind.Go, goModule)
            : null;
    }
}
