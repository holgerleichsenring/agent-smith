using System.Reflection;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0325: reads the skills release baked into this assembly. The tarball is
/// embedded by the <c>EmbedSkillsCatalog</c> MSBuild step in
/// AgentSmith.Infrastructure.Core.csproj (pinned via
/// <c>SkillsCatalogVersion</c>, SHA256-verified, master descriptions
/// validated at build time), so every binary that composes the skills
/// services — CLI, Server, docker images — carries the exact catalog it was
/// tested with.
/// </summary>
public sealed class EmbeddedSkillsCatalog : IEmbeddedSkillsCatalog
{
    internal const string ResourceName = "AgentSmith.SkillsCatalog.tar.gz";
    private const string VersionMetadataKey = "SkillsCatalogVersion";

    public string Version { get; } = ReadVersion();

    public Stream Open() =>
        typeof(EmbeddedSkillsCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded skills catalog resource '{ResourceName}' not found in " +
                "AgentSmith.Infrastructure.Core — the EmbedSkillsCatalog build step did not run.");

    private static string ReadVersion() =>
        typeof(EmbeddedSkillsCatalog).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == VersionMetadataKey)?.Value
            ?? throw new InvalidOperationException(
                $"AssemblyMetadata '{VersionMetadataKey}' not found in AgentSmith.Infrastructure.Core — " +
                "the SkillsCatalogVersion pin is missing from the project file.");
}
