using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-19-4c1f: reads one repository's authored rules through its own
/// <see cref="ISourceProvider"/> — one directory listing and a handful of file reads on
/// the remote, which need no container and no clone. <c>SandboxLanguageResolver</c> is
/// the precedent: it walks the same contexts directory before any sandbox exists.
/// <para>
/// The provider's file read takes a path and nothing else, so it lands on the remote's
/// own default branch — the same ref the turn's lazy scope clones when no revision is
/// named. A declared TEMPLATE carries a revision that a remote read cannot honour, which
/// is why the grounding covers the scope's repositories and not its templates.
/// </para>
/// </summary>
public sealed class DialogGroundingReader(
    ISourceProviderFactory sourceProviders,
    IContextYamlParser contextYaml,
    ILogger<DialogGroundingReader> logger)
{
    public async Task<DialogGrounding> ReadAsync(RepoConnection repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (!repo.HasLocation) return DialogGrounding.NotLocated(repo.Name);

        ISourceProvider provider;
        IReadOnlyList<string> contextNames;
        try
        {
            provider = sourceProviders.Create(repo);
            contextNames = await provider.ListDirectoryAsync(ProjectMetaPaths.Contexts, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Grounding {Repo}: listing {Path} failed.",
                repo.Name, ProjectMetaPaths.Contexts);
            return DialogGrounding.Unreached(repo.Name, ex.Message);
        }

        var contexts = await ReadContextsAsync(provider, repo.Name, contextNames, cancellationToken);
        var principles = await ReadPrinciplesAsync(provider, repo.Name, contexts.Documents, cancellationToken);
        return new DialogGrounding(repo.Name, true, null, contexts, principles);
    }

    private async Task<RemoteFileSet> ReadContextsAsync(
        ISourceProvider provider, string repo, IReadOnlyList<string> names, CancellationToken ct)
    {
        var documents = new List<ContextDocument>();
        var unreadable = new List<string>();
        foreach (var name in names)
        {
            var path = $"{ProjectMetaPaths.Contexts}/{name}/{ProjectMetaPaths.ContextYamlFile}";
            var yaml = await TryReadAsync(provider, repo, path, unreadable, ct);
            if (yaml is null) continue;
            documents.Add(new ContextDocument(repo, name, Workdir(yaml), path, yaml));
        }
        return new RemoteFileSet(documents, unreadable);
    }

    // The flat file speaks for the whole repository, exactly as the sandbox-bound loader
    // reads it — so the fan-out over the contexts happens only when the probe finds none.
    private async Task<RemoteFileSet> ReadPrinciplesAsync(
        ISourceProvider provider, string repo,
        IReadOnlyList<ContextDocument> contexts, CancellationToken ct)
    {
        var unreadable = new List<string>();
        var flat = await TryReadAsync(provider, repo, ProjectMetaPaths.Principles, unreadable, ct);
        if (flat is not null)
            return new RemoteFileSet(
                [new ContextDocument(repo, null, null, ProjectMetaPaths.Principles, flat)], unreadable);

        var documents = new List<ContextDocument>();
        foreach (var context in contexts)
        {
            var path = $"{ProjectMetaPaths.Contexts}/{context.ContextName}/{ProjectMetaPaths.PrinciplesFile}";
            var content = await TryReadAsync(provider, repo, path, unreadable, ct);
            if (content is null) continue;
            documents.Add(new ContextDocument(repo, context.ContextName, context.Workdir, path, content));
        }
        return new RemoteFileSet(documents, unreadable);
    }

    // The provider contract answers an absent path with null and propagates auth and
    // transport errors, so a throw here is a file that could not be READ — a different
    // answer from "it is not there", and the report has to keep it different.
    private async Task<string?> TryReadAsync(
        ISourceProvider provider, string repo, string path, List<string> unreadable, CancellationToken ct)
    {
        try
        {
            var content = await provider.TryReadFileAsync(path, ct);
            return string.IsNullOrEmpty(content) ? null : content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Grounding {Repo}: read of {Path} failed.", repo, path);
            unreadable.Add($"{path} ({ex.Message})");
            return null;
        }
    }

    // meta.workdir labels the section the master reads. A context.yaml that does not parse
    // still SPEAKS to a design conversation, so the document is kept and only its label
    // goes unset — the toolchain decisions that need a parsed workdir are not made here.
    private string? Workdir(string yaml)
    {
        try { return contextYaml.Parse(yaml).Summary?.Workdir; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Grounding: context.yaml did not parse; its label carries no workdir.");
            return null;
        }
    }
}
