using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0513: the domain profiles as the PINNED CATALOG ships them, read through the
/// same parser a run uses — so the gate polices the artefact that reaches a
/// sandbox, not a second copy of the YAML written in this repository.
/// <para>
/// Armed at the pin, like p0504's packaging test: a profile authored in the skills
/// catalog only exists in a binary once <c>SkillsCatalogVersion</c> names a release
/// that carries it. Below that pin <see cref="Find"/> returns null and the callers
/// state what is still outstanding instead of asserting over an empty tarball.
/// </para>
/// </summary>
public sealed class PackagedProfiles : IDisposable
{
    /// <summary>The first catalog release that ships <c>profiles/</c> (p0504).</summary>
    public static readonly Version ProfilesFrom = new(4, 7, 0);

    private readonly EmbeddedSkillsCatalog _catalog = new();
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "agentsmith-p0513-" + Guid.NewGuid().ToString("N"));

    public Version Pin => Version.Parse(_catalog.Version.TrimStart('v', 'V'));

    /// <summary>True once the pinned catalog is new enough to carry profiles at all.</summary>
    public bool Armed => Pin >= ProfilesFrom;

    public DomainProfile? Find(string domain)
    {
        Extract();
        return new FileDomainProfileCatalog(
            new PinnedCatalogPath(_root), NullLogger<FileDomainProfileCatalog>.Instance).Find(domain);
    }

    /// <summary>Every domain the pinned catalog carries, read through the production catalog.</summary>
    public IReadOnlyList<string> KnownDomains()
    {
        Extract();
        return new FileDomainProfileCatalog(
            new PinnedCatalogPath(_root), NullLogger<FileDomainProfileCatalog>.Instance).KnownDomains;
    }

    /// <summary>Every entry name in the pinned tarball, so a below-pin test can still assert.</summary>
    public IReadOnlyList<string> Entries()
    {
        using var reader = OpenTar(out var gz, out var tarball);
        var names = new List<string>();
        while (reader.GetNextEntry() is { } entry) names.Add(entry.Name.TrimStart('.', '/'));
        gz.Dispose();
        tarball.Dispose();
        return names;
    }

    private void Extract()
    {
        if (Directory.Exists(_root)) return;
        Directory.CreateDirectory(_root);
        using var reader = OpenTar(out var gz, out var tarball);
        while (reader.GetNextEntry() is { } entry)
        {
            var name = entry.Name.TrimStart('.', '/');
            if (!name.StartsWith("profiles/", StringComparison.Ordinal) || entry.DataStream is null)
                continue;
            var target = Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var file = File.Create(target);
            entry.DataStream.CopyTo(file);
        }
        gz.Dispose();
        tarball.Dispose();
    }

    private TarReader OpenTar(out GZipStream gz, out Stream tarball)
    {
        tarball = _catalog.Open();
        gz = new GZipStream(tarball, CompressionMode.Decompress);
        return new TarReader(gz);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record PinnedCatalogPath(string Root) : ISkillsCatalogPath
    {
        public string Origin => $"pinned catalog {Root}";
    }
}
