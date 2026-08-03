using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: reads the spec set back off the checked-out ticket branch — the FIRST
/// source in the precedence, ahead of an embedded spec and ahead of deriving one.
/// A missing or unreadable index is "no set on the branch", never a failure: the
/// run then derives.
/// </summary>
public sealed class SpecSetReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    PhaseDraftReader draftReader,
    ILogger<SpecSetReader> logger) : ISpecSetReader
{
    public async Task<SpecSetReadResult?> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSetKey key,
        CancellationToken cancellationToken)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
        {
            logger.LogDebug("No sandbox for {Repo} — no spec set to read", carryingRepo.Name);
            return null;
        }

        var sandbox = matches[0].Value;
        var files = readerFactory.Create(sandbox);
        var doc = SpecSetIndex.Parse(
            await files.TryReadAsync($"{key.Directory}/{SpecSetIndex.FileName}", cancellationToken));
        if (doc is null) return null;

        var phases = new List<SpecPhase>(doc.Phases.Count);
        foreach (var stem in doc.Phases)
        {
            var phase = await ReadPhaseAsync(files, key, stem, doc, cancellationToken);
            if (phase is null)
            {
                logger.LogWarning(
                    "{Path} is listed in {Index} but did not read back as a phase spec — "
                    + "the branch artifact is ignored and the set is derived again",
                    key.YamlPath(stem), SpecSetIndex.FileName);
                return null;
            }
            phases.Add(phase);
        }

        var sha = await gitOps.GetLastCommitForPathAsync(sandbox, key.Directory, cancellationToken);
        var set = new SpecSet(
            doc.Key.Length > 0 ? doc.Key : key.Value,
            phases,
            SpecSetIndex.AccountingOf(doc),
            SpecSetIndex.RevisionsOf(doc),
            SpecSource.BranchArtifact,
            SpecSetIndex.HandbackOf(doc),
            doc.TicketPinnedWhole,
            doc.ExecutedPhases);
        logger.LogInformation(
            "Spec set {Key} read from the ticket branch: {Phases} phase(s), revision {Revision}",
            set.Key, set.Phases.Count, set.Current.Number);
        return new SpecSetReadResult(set, sha);
    }

    private async Task<SpecPhase?> ReadPhaseAsync(
        ISandboxFileReader files, SpecSetKey key, string stem,
        SpecSetIndexDocument doc, CancellationToken ct)
    {
        var yaml = await files.TryReadAsync(key.YamlPath(stem), ct);
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        var markdown = await files.TryReadAsync(key.MarkdownPath(stem), ct) ?? string.Empty;
        try
        {
            var draft = draftReader.Read(yaml!);
            var carried = doc.Carried
                .Where(c => string.Equals(c.Phase, draft.PhaseId, StringComparison.Ordinal))
                .Select(c => c.Segment)
                .ToList();
            return new SpecPhase(draft, SlugOf(stem, draft.PhaseId), markdown, carried);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Path} is not a readable phase spec", key.YamlPath(stem));
            return null;
        }
    }

    private static string SlugOf(string stem, string phaseId) =>
        stem.StartsWith($"{phaseId}-", StringComparison.Ordinal)
            ? stem[(phaseId.Length + 1)..]
            : stem;
}
