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
/// 2026-08-28-3302: no longer armed at the pin. The embedded catalog carries the
/// profiles, so a null lookup is a defect to assert on rather than a state to skip
/// over; <see cref="ProfilesFrom"/> survives as the floor a failure message names.
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
