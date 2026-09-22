using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-b6ad: the files a spec-set directory HOLDS, rendered in one place for the two
/// writers that put them there — the run's publish inside a sandbox, and filing's checkout-free
/// write onto the ticket branch.
/// <para>
/// One renderer, because the first run after a filing must find the directory it would itself have
/// written. A writer that rendered a subset would leave the other staging the rest and committing
/// a second revision over a set nobody changed — the no-op is a property of the CONTENT being
/// identical, and two renderers is how two contents come to differ.
/// </para>
/// <para>
/// Four kinds: the index the next run reads the sequence from, one schema-valid yaml per phase,
/// its markdown companion, and the accounting a reviewer reads in the pull request.
/// </para>
/// </summary>
public sealed class SpecSetFiles(SpecSetIndex index)
{
    /// <summary>Repo-relative path and whole content, in the order a writer may commit them.</summary>
    public IReadOnlyList<SpecSetFile> Render(
        SpecSetKey key, SpecSet set, IReadOnlyList<TicketSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(set);
        var files = new List<SpecSetFile>(2 + (set.Phases.Count * 2))
        {
            new($"{key.Directory}/{SpecSetIndex.FileName}", index.Serialize(set)),
        };
        foreach (var phase in set.Phases)
        {
            files.Add(new SpecSetFile(
                key.YamlPath(phase.FileStem), phase.Draft.Yaml.TrimEnd() + "\n"));
            files.Add(new SpecSetFile(key.MarkdownPath(phase.FileStem), phase.Markdown));
        }
        files.Add(new SpecSetFile(
            key.AccountingPath, SpecAccountingBuilder.Render(set.Accounting, segments, set.Key)));
        return files;
    }
}

/// <summary>2026-09-22-b6ad: one rendered file of a spec-set directory.</summary>
public sealed record SpecSetFile(string Path, string Content);
