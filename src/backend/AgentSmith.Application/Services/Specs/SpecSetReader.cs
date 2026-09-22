using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: reads the spec set back off the checked-out ticket branch — the FIRST
/// source in the precedence, ahead of an embedded spec and ahead of deriving one.
/// <para>
/// 2026-09-22-6ad7: it says WHICH nothing it found. An absent index is a branch nobody has
/// written the set to yet; an index that does not parse, a listed phase file that does not read
/// back, and a carrying repository this run never checked out are all "something is there and
/// this run cannot read it" — the caller hands those back rather than replacing them from a copy.
/// </para>
/// </summary>
public sealed class SpecSetReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SpecSetPhaseFileReader phases,
    SpecSetIndex index,
    SandboxTargets sandboxTargets,
    ILogger<SpecSetReader> logger) : ISpecSetReader
{
    public async Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSetKey key,
        CancellationToken cancellationToken)
    {
        var matches = sandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
        {
            // NOT "nothing at the path": the path was never looked at. A branch this run cannot
            // see may well carry the set, so the copy must not stand in for it.
            logger.LogWarning(
                "No sandbox for {Repo} — the repository carrying the spec set is not checked out",
                carryingRepo.Name);
            return SpecSetOnBranch.Unreadable(
                $"the repository '{carryingRepo.Name}' carrying the set is not checked out in this run");
        }

        var sandbox = matches[0].Value;
        var files = readerFactory.Create(sandbox);
        var indexPath = $"{key.Directory}/{SpecSetIndex.FileName}";
        var doc = index.Parse(
            await files.TryReadAsync(indexPath, cancellationToken));
        if (doc is null) return await AbsentOrBrokenAsync(files, indexPath, cancellationToken);

        var read = new List<SpecPhase>(doc.Phases.Count);
        foreach (var stem in doc.Phases)
        {
            var phase = await phases.ReadAsync(files, key, stem, doc, cancellationToken);
            if (phase is null)
            {
                logger.LogWarning(
                    "{Path} is listed in {Index} but did not read back as a phase spec",
                    key.YamlPath(stem), SpecSetIndex.FileName);
                return SpecSetOnBranch.Unreadable(
                    $"{key.YamlPath(stem)} is listed in {SpecSetIndex.FileName} and did not read "
                    + "back as a phase spec");
            }
            read.Add(phase);
        }

        var sha = await gitOps.GetLastCommitForPathAsync(sandbox, key.Directory, cancellationToken);
        var set = new SpecSet(
            doc.Key.Length > 0 ? doc.Key : key.Value,
            read,
            index.AccountingOf(doc),
            index.RevisionsOf(doc),
            SpecSource.BranchArtifact,
            index.HandbackOf(doc),
            doc.TicketPinnedWhole,
            doc.ExecutedPhases,
            index.FingerprintOf(doc),
            index.ApprovalOf(doc));
        logger.LogInformation(
            "Spec set {Key} read from the ticket branch: {Phases} phase(s), revision {Revision}",
            set.Key, set.Phases.Count, set.Current.Number);
        return SpecSetOnBranch.Answered(new SpecSetReadResult(set, sha));
    }

    // An index that is not THERE is a hand-off nobody has made; an index that is there and did
    // not parse is an edit that broke it, and the two must not be answered alike.
    private async Task<SpecSetOnBranch> AbsentOrBrokenAsync(
        ISandboxFileReader files, string indexPath, CancellationToken ct)
    {
        if (!await files.ExistsAsync(indexPath, ct)) return SpecSetOnBranch.Nothing;
        logger.LogWarning("{Path} is on the ticket branch and did not parse", indexPath);
        return SpecSetOnBranch.Unreadable($"{indexPath} is on the ticket branch and did not parse");
    }
}
