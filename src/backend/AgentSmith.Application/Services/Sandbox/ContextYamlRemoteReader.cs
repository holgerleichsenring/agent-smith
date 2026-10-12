using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// One context's context.yaml read through the source provider — on a named branch, or on the
/// default branch when none is given — and parsed to the summary discovery needs. Every way it can
/// fail answers null with a warning naming the reason, because one unreadable context must not
/// cost the others their sandbox.
/// </summary>
public sealed class ContextYamlRemoteReader(
    IContextYamlParser contextYamlParser, ILogger<ContextYamlRemoteReader> logger)
{
    public async Task<ContextYamlSummary?> ReadAsync(
        ISourceProvider provider, BranchName? branch, string repoTag, string contextName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var path = $"{ContextBranchLocator.ContextsRoot}/{contextName}/context.yaml";
        string? yaml;
        try
        {
            yaml = branch is null
                ? await provider.TryReadFileAsync(path, ct)
                : await provider.TryReadFileAsync(path, branch, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Context {Repo}/{Context}: read {Path} threw. Skipped.", repoTag, contextName, path);
            return null;
        }

        if (string.IsNullOrEmpty(yaml))
        {
            logger.LogWarning("Context {Repo}/{Context}: read {Path} returned empty. Skipped.", repoTag, contextName, path);
            return null;
        }
        return Parse(yaml, repoTag, contextName, path);
    }

    private ContextYamlSummary? Parse(string yaml, string repoTag, string contextName, string path)
    {
        ContextYamlParseResult result;
        try
        {
            result = contextYamlParser.Parse(yaml);
        }
        catch (InvalidOperationException ex)
        {
            // meta.workdir is required — surface as a config error.
            logger.LogWarning("Context {Repo}/{Context}: {Path} rejected — {Reason}. Skipped.",
                repoTag, contextName, path, ex.Message);
            return null;
        }
        if (result.ErrorReason is not null || result.Summary is null)
        {
            logger.LogWarning(
                "Context {Repo}/{Context}: {Path} parse failed — {Reason} ({Bytes} bytes read). Skipped "
                + "(sandbox will fall back to generic image).",
                repoTag, contextName, path, result.ErrorReason ?? "no summary: shape did not match", yaml.Length);
            return null;
        }
        var summary = result.Summary;
        logger.LogInformation("Context {Repo}/{Context}: workdir={Workdir} lang={Lang} image={Image}",
            repoTag, contextName, summary.Workdir, summary.Language ?? "null", summary.Image ?? "null");
        return summary;
    }
}
