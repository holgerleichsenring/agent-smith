using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0161d: read-only first pass of cold-init. Resolves the project-discovery
/// skill (output_schema=discovery) from AvailableRoles, builds a read-only
/// tool-bearing chat call per RepoConnection, parses the LLM's structured
/// discovery output, and publishes
/// <see cref="ContextKeys.DiscoveredComponents"/> for BootstrapDispatchHandler
/// to fan out. Re-init short-circuits when SandboxDiscoveries already
/// surfaces a non-synthetic context (real <c>.agentsmith/contexts/&lt;name&gt;/</c>
/// dirs existed on the remote).
///
/// Ambiguity: interactive transports get <c>ask_human</c>; headless runs
/// fail loud (sets <see cref="ContextKeys.DiscoveryAmbiguous"/>) so
/// BootstrapDispatchHandler refuses to emit any round.
/// </summary>
public sealed class BootstrapDiscoverHandler(
    IChatClientFactory chatClientFactory,
    IDialogueTransport? dialogueTransport,
    IRunContextAccessor runContext,
    ILogger<BootstrapDiscoverHandler> logger)
    : ICommandHandler<BootstrapDiscoverContext>
{
    private const string DiscoverySkillSchema = "discovery";
    private const string SyntheticDefaultName = "default";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapDiscoverContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos)
            || repos is null || repos.Count == 0)
            return CommandResult.Fail("BootstrapDiscover: no Repos in pipeline context");

        if (TrySkipFromExistingDiscoveries(pipeline, repos, out var skipResult))
            return skipResult;

        if (!TryResolveDiscoverySkill(pipeline, out var skill, out var resolveError))
            return CommandResult.Fail(resolveError);
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return CommandResult.Fail("BootstrapDiscover: no Repository in pipeline context");

        var perRepo = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(
            repos.Count, StringComparer.Ordinal);
        foreach (var repo in repos)
        {
            var result = await DiscoverOneAsync(
                context, skill, repository, repo, cancellationToken);
            if (!result.Success) return result.Failure!;
            perRepo[repo.Name] = result.Components!;
        }

        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, perRepo);
        var summary = string.Join(", ", perRepo.Select(kv => $"{kv.Key}:{kv.Value.Count}"));
        return CommandResult.Ok($"BootstrapDiscover: discovered components [{summary}]");
    }

    private bool TrySkipFromExistingDiscoveries(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, out CommandResult skipResult)
    {
        skipResult = CommandResult.Ok("BootstrapDiscover: skipped");
        if (!pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
                ContextKeys.SandboxDiscoveries, out var discoveries) || discoveries is null)
            return false;
        if (!discoveries.Values.Any(IsRealDiscovery))
            return false;

        var perRepo = ProjectExistingDiscoveriesPerRepo(repos, discoveries);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, perRepo);
        var summary = string.Join(", ", perRepo.Select(kv => $"{kv.Key}:{kv.Value.Count}"));
        skipResult = CommandResult.Ok(
            $"BootstrapDiscover: re-init — projected existing contexts/ ({summary})");
        logger.LogInformation(
            "BootstrapDiscover: re-init path — projected {Count} existing context(s) into DiscoveredComponents",
            perRepo.Values.Sum(v => v.Count));
        return true;
    }

    private static bool IsRealDiscovery(RemoteContextDiscovery d) =>
        !(d.ContextName == SyntheticDefaultName && d.Workdir == "." && d.Language is null);

    private static IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>
        ProjectExistingDiscoveriesPerRepo(
            IReadOnlyList<RepoConnection> repos,
            IReadOnlyDictionary<string, RemoteContextDiscovery> discoveries)
    {
        var perRepo = new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(
            repos.Count, StringComparer.Ordinal);
        var multiRepo = repos.Count > 1;
        foreach (var repo in repos)
        {
            var matching = discoveries
                .Where(kv => BelongsToRepo(kv.Key, repo.Name, multiRepo))
                .Select(kv => ToComponent(kv.Value))
                .ToList();
            perRepo[repo.Name] = matching;
        }
        return perRepo;
    }

    private static bool BelongsToRepo(string sandboxKey, string repoName, bool multiRepo) =>
        !multiRepo
        || sandboxKey == repoName
        || sandboxKey.StartsWith(repoName + "/", StringComparison.Ordinal);

    private static DiscoveredComponent ToComponent(RemoteContextDiscovery d) =>
        new(d.ContextName, d.Workdir, d.Language ?? string.Empty, $".agentsmith/contexts/{d.ContextName}/context.yaml");

    private static bool TryResolveDiscoverySkill(
        PipelineContext pipeline, out RoleSkillDefinition skill, out string error)
    {
        skill = null!;
        error = string.Empty;
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
        { error = "BootstrapDiscover: no AvailableRoles loaded — run LoadSkills first"; return false; }
        var match = roles.Where(r => r.OutputSchema == DiscoverySkillSchema).ToList();
        if (match.Count == 0)
        { error = "BootstrapDiscover: no skill with output_schema=discovery available"; return false; }
        if (match.Count > 1)
        {
            error =
                $"BootstrapDiscover: ambiguous discovery skill — got {match.Count} ({string.Join(", ", match.Select(s => s.Name))}); expected exactly one";
            return false;
        }
        skill = match[0];
        return true;
    }

    private async Task<DiscoverResult> DiscoverOneAsync(
        BootstrapDiscoverContext context, RoleSkillDefinition skill,
        Repository repository, RepoConnection repo, CancellationToken ct)
    {
        var sandbox = ResolveSandbox(context.Pipeline, repo.Name);
        if (sandbox is null)
            return DiscoverResult.Failed(CommandResult.Fail(
                $"BootstrapDiscover: no sandbox available for repo '{repo.Name}'"));
        var projectMap = ResolveProjectMap(context.Pipeline, repo.Name);
        if (projectMap is null)
            return DiscoverResult.Failed(CommandResult.Fail(
                $"BootstrapDiscover: no ProjectMap available for repo '{repo.Name}'"));

        var tools = BuildTools(sandbox, repository);
        var isInteractive = dialogueTransport is not null;
        var (system, user) = BootstrapDiscoverPromptFactory.Build(
            skill, repository, repo.Name, projectMap, isInteractive);
        var responseText = await CallSkillAsync(context, skill, system, user, tools, repo.Name, ct);

        return ParseAndProject(repo, responseText, context.Pipeline);
    }

    private IList<AITool> BuildTools(ISandbox sandbox, Repository repository)
    {
        var fs = new FilesystemToolHost(sandbox, repository.LocalPath);
        var human = new HumanToolHost(dialogueTransport);
        return AgenticToolSurface.BootstrapDiscover(fs, human);
    }

    private async Task<string> CallSkillAsync(
        BootstrapDiscoverContext context, RoleSkillDefinition skill,
        string system, string user, IList<AITool> tools, string repoName, CancellationToken ct)
    {
        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Primary);
        var options = new ChatOptions { Tools = tools, MaxOutputTokens = maxTokens };
        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var roleName = skill.Role ?? "producer";
        using var _ = costTracker.BeginCall(
            skill.Name, roleName, SkillExecutionPhase.BootstrapDiscover, repoName);
        using var _scope = runContext.BeginCallScope(
            roleName, SkillExecutionPhase.BootstrapDiscover.ToString(), repoName);
        var response = await chat.GetResponseAsync(
            [new(ChatRole.System, system), new(ChatRole.User, user)], options, ct);
        costTracker.Track(response);
        return response.Text ?? string.Empty;
    }

    private DiscoverResult ParseAndProject(
        RepoConnection repo, string responseText, PipelineContext pipeline)
    {
        var parsed = DiscoveryOutputParser.TryParse(responseText, out var output, out var parseError);
        if (!parsed)
            return DiscoverResult.Failed(CommandResult.Fail(
                $"BootstrapDiscover: repo '{repo.Name}' output parse failed — {parseError}"));
        if (output!.Status == "ambiguous")
        {
            var message = BuildAmbiguityMessage(repo.Name, output);
            pipeline.Set(ContextKeys.DiscoveryAmbiguous, message);
            logger.LogWarning(
                "BootstrapDiscover: repo {Repo} returned status=ambiguous — {Message}", repo.Name, message);
            return DiscoverResult.Failed(CommandResult.Fail(message));
        }
        logger.LogInformation(
            "BootstrapDiscover: repo {Repo} → {Count} component(s) ({Slugs})",
            repo.Name, output.Components.Count,
            string.Join(", ", output.Components.Select(c => c.Name)));
        return DiscoverResult.Ok(output.Components);
    }

    private static string BuildAmbiguityMessage(string repoName, DiscoveryOutput output)
    {
        var candidates = output.Ambiguity?.Candidates is { Count: > 0 } c
            ? string.Join(", ", c)
            : "(no candidates listed)";
        var detail = output.Ambiguity?.Message ?? "no detail provided";
        return $"BootstrapDiscover: repo '{repoName}' is ambiguous — {detail}. Candidates: [{candidates}]. " +
               "Re-run init-project via the CLI for interactive disambiguation.";
    }

    private static ISandbox? ResolveSandbox(PipelineContext pipeline, string repoName)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var dict) || dict is null)
            return null;
        if (dict.TryGetValue(repoName, out var exact)) return exact;
        return dict.Values.FirstOrDefault();
    }

    private static ProjectMap? ResolveProjectMap(PipelineContext pipeline, string repoName)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is not null
            && dict.TryGetValue(repoName, out var perRepo))
            return perRepo;
        return pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var legacy) ? legacy : null;
    }

    private readonly record struct DiscoverResult(
        bool Success, IReadOnlyList<DiscoveredComponent>? Components, CommandResult? Failure)
    {
        public static DiscoverResult Ok(IReadOnlyList<DiscoveredComponent> components)
            => new(true, components, null);
        public static DiscoverResult Failed(CommandResult failure)
            => new(false, null, failure);
    }
}

/// <summary>
/// p0161d: parses the project-discovery skill's JSON output into a typed
/// <see cref="DiscoveryOutput"/> record. Tolerates a leading/trailing
/// markdown code fence (the LLM occasionally adds one despite the prompt
/// asking for raw JSON). All other malformed output is rejected loud.
/// </summary>
internal static class DiscoveryOutputParser
{
    public static bool TryParse(string raw, out DiscoveryOutput? output, out string error)
    {
        output = null;
        error = string.Empty;
        var json = StripCodeFence(raw);
        if (string.IsNullOrWhiteSpace(json))
        { error = "empty discovery output"; return false; }
        try
        {
            output = JsonSerializer.Deserialize<DiscoveryOutput>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (output is null) { error = "JSON deserialized to null"; return false; }
            if (string.IsNullOrEmpty(output.Status))
            { error = "missing status field"; return false; }
            return true;
        }
        catch (JsonException ex) { error = ex.Message; return false; }
    }

    private static string StripCodeFence(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        var withoutOpen = trimmed[(firstNewline + 1)..];
        var lastFence = withoutOpen.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence < 0 ? withoutOpen : withoutOpen[..lastFence].TrimEnd();
    }
}

/// <summary>Typed projection of the discovery output_schema (status +
/// components[] + optional ambiguity). Internal — only the handler + parser
/// touch this shape.</summary>
internal sealed class DiscoveryOutput
{
    public string Status { get; set; } = string.Empty;
    public List<DiscoveredComponent> Components { get; set; } = [];
    public DiscoveryAmbiguity? Ambiguity { get; set; }
}

internal sealed class DiscoveryAmbiguity
{
    public string Message { get; set; } = string.Empty;
    public List<string> Candidates { get; set; } = [];
}
