using System.Collections.Concurrent;
using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-11-04-96a0c: what earlier turns opened of each upload of a conversation, as a design turn begins. M counts
/// the files read_file can open, by read_file's own rule — 1 MB or under, and bytes that decode as strict UTF-8 — since
/// a stored media type cannot say: source code and PDFs are both octet-stream. Only files of 1 MB or under have their
/// content loaded. Which files are readable is cached per set and file count, so a set re-read on every turn is
/// decoded once; a file removed from the set changes the count and the key.
/// </summary>
public sealed class ReferenceCoverageReader(IServiceScopeFactory scopeFactory)
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly ConcurrentDictionary<(string SetId, int Files), (IReadOnlySet<string> Readable, int Unreadable)> _readable = new();

    /// <summary>Each set's coverage, by set id.</summary>
    public async Task<IReadOnlyDictionary<string, ReferenceCoverage>> ForAsync(
        string conversationId, IEnumerable<ReferenceSetSummary> sets, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sets);
        await using var scope = scopeFactory.CreateAsyncScope();
        var files = scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>();
        var reads = await scope.ServiceProvider.GetRequiredService<ReferenceReadRepository>().ReadsAsync(conversationId, ct);
        var coverage = new Dictionary<string, ReferenceCoverage>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            var (readable, unreadable) = await ReadableAsync(files, conversationId, set, ct);
            var opened = reads.GetValueOrDefault(set.SetId)?.Where(readable.Contains).ToHashSet(StringComparer.Ordinal)
                ?? [];
            coverage[set.SetId] = new ReferenceCoverage(readable, opened, unreadable);
        }
        return coverage;
    }

    private async Task<(IReadOnlySet<string> Readable, int Unreadable)> ReadableAsync(
        ReferenceSetRepository files, string conversationId, ReferenceSetSummary set, CancellationToken ct)
    {
        if (_readable.TryGetValue((set.SetId, set.Files), out var known)) return known;
        var small = await files.SmallFilesAsync(conversationId, set.SetId, SizeLimits.ReadFileMaxBytes, ct);
        var readable = small.Where(file => Decodes(file.Content)).Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        return _readable[(set.SetId, set.Files)] = (readable, set.Files - readable.Count);
    }

    /// <summary>read_file's own test: the bytes after a UTF-8 byte-order mark decode strictly.</summary>
    private static bool Decodes(byte[] content)
    {
        var bom = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
        try
        {
            _ = Strict.GetString(content, bom, content.Length - bom);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
