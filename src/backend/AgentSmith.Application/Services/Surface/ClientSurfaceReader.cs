using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: ONE read-only call that reads the declared client checkouts and reports
/// the call sites it found. The reader is a model, not a parser — a per-language route and
/// call-site extractor is the pattern this project has already refused, and it would decide
/// only the frameworks somebody wrote a rule for.
/// <para>
/// The account is assembled here rather than asked for: the files READ come from the tool
/// surface's own read-set, so an over-confident report cannot widen its own coverage. Only
/// the undecided files are the reader's to name, and naming them costs it nothing.
/// </para>
/// </summary>
public sealed class ClientSurfaceReader(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    AgenticToolSurface toolSurface,
    LoopLimitsConfig limits,
    ILogger<ClientSurfaceReader> logger) : IClientSurfaceReader
{
    public const string RoleName = "client-surface-reader";

    public async Task<ClientUsageReport?> ReadAsync(
        ClientSurfaceRequest request, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(costTracker);
        var fs = new FilesystemToolHost(
            request.Sandboxes, request.DefaultKey, request.RepoPath,
            keyToRepo: request.KeyToRepo, logger: logger);
        try
        {
            var text = await AskAsync(request, fs, costTracker, cancellationToken);
            var reported = ClientUsageReportReader.Read(text);
            if (reported is null)
            {
                logger.LogWarning(
                    "The client call-site reading produced no readable report — the difference "
                    + "is not computed rather than computed over nothing");
                return null;
            }
            return Report(reported, fs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The client call-site reading failed — no difference is claimed");
            return null;
        }
    }

    private async Task<string?> AskAsync(
        ClientSurfaceRequest request, FilesystemToolHost fs,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(
            request.Agent, TaskType.Scout, maxIterations: limits.MaxToolCallsPerSkill);
        var options = new ChatOptions
        {
            Tools = toolSurface.Scout(fs),
            MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(request.Agent, TaskType.Scout),
        };
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ClientUsagePrompt.System()),
            new(ChatRole.User, ClientUsagePrompt.User(request.ConsumerRepos, request.Served)),
        };

        using var timeBound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeBound.CancelAfter(TimeSpan.FromSeconds(limits.MaxSecondsPerSkillCall));
        using var call = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Investigate, RoleName);
        using var scope = runContext.BeginCallScope(
            RoleName, SkillExecutionPhase.Investigate.ToString(), RoleName);
        var response = await chat.GetResponseAsync(messages, options, timeBound.Token);
        costTracker.Track(response);
        return response.Text;
    }

    private ClientUsageReport Report(ReportedClientUsage reported, FilesystemToolHost fs)
    {
        var account = new ClientExtractionAccount(
            [.. fs.ReadPaths], reported.Undecided, reported.CallSites.Count);
        logger.LogInformation(
            "Client call sites: {Sites} found across {Read} file(s) read, {Undecided} file(s) not decided",
            account.CallSitesFound, account.FilesRead.Count, account.FilesNotDecided.Count);
        return new ClientUsageReport(reported.CallSites, account);
    }
}
