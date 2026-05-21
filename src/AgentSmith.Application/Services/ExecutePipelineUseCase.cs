using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Central entry point: takes a PipelineRequest, loads config,
/// resolves the pipeline, and executes it end-to-end.
/// Also supports legacy string-based input for backward compatibility.
/// </summary>
public sealed class ExecutePipelineUseCase(
    IConfigurationLoader configLoader,
    IIntentParser intentParser,
    IPipelineExecutor pipelineExecutor,
    ISourceConfigOverrider sourceConfigOverrider,
    ISkillsCatalogResolver catalogResolver,
    ISkillsCatalogPath catalogPath,
    ISkillLoader skillLoader,
    IPipelineConfigResolver pipelineConfigResolver,
    ILogger<ExecutePipelineUseCase> logger)
{
    /// <summary>
    /// Sub-path inside the catalog root where the shared concept vocabulary lives.
    /// The vocabulary is global per catalog (not per-pipeline-skills-path), so it
    /// always sits at the top of the <c>skills/</c> tree regardless of which
    /// pipeline runs.
    /// </summary>
    private const string CatalogSkillsRootSubPath = "skills";

    public async Task<CommandResult> ExecuteAsync(
        PipelineRequest request, string configPath, CancellationToken cancellationToken)
    {
        var runStartedAt = DateTimeOffset.UtcNow;
        var runId = RunIdGenerator.Generate(runStartedAt);
        using var logScope = logger.BeginScope("run={RunId}", runId);

        var ticketDesc = request.TicketId is not null ? $" ticket #{request.TicketId.Value}" : "";
        logger.LogInformation(
            "Executing pipeline '{Pipeline}' for project '{Project}'{TicketDesc} (run {RunId})",
            request.PipelineName, request.ProjectName, ticketDesc, runId);

        var config = configLoader.LoadConfig(configPath);
        await catalogResolver.EnsureResolvedAsync(config.Skills, cancellationToken);

        var projectName = request.ProjectName.ToLowerInvariant();
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException($"Project '{projectName}' not found in configuration.");

        var repos = ResolveRepos(projectConfig, request.Context);

        var commands = PipelinePresets.TryResolve(request.PipelineName)
            ?? throw new ConfigurationException($"Pipeline '{request.PipelineName}' not found in presets.");

        var resolved = pipelineConfigResolver.Resolve(projectConfig, request.PipelineName);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, runId);
        pipeline.Set(ContextKeys.RunStartedAt, runStartedAt);
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, repos);
        pipeline.Set(ContextKeys.ResolvedPipeline, resolved);
        pipeline.Set(ContextKeys.Headless, request.Headless);
        pipeline.Set(ContextKeys.PipelineTypeName, PipelinePresets.GetPipelineType(request.PipelineName));
        pipeline.Set(ContextKeys.PipelineName, request.PipelineName);
        pipeline.Set(ContextKeys.ConfigDir, Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".");
        pipeline.Set("ProjectPricing", resolved.Agent.Pricing);
        pipeline.Set("PipelineCostCap", config.PipelineCostCap.ResolveFor(request.PipelineName));

        // p0125c-followup: vocabulary must be in PipelineContext BEFORE the first
        // handler runs. Since p0125c, PipelineNameInitializer is step 1 of every
        // preset and calls SetEnum("pipeline_name", ...) — which throws
        // KeyNotFoundException against ConceptVocabulary.Empty. LoadSkills
        // populates the vocabulary too, but it's much later in every preset
        // (typically step 14+), so the early concept-writers had no vocab.
        // Loading here, between catalog-resolve and pipeline-execute, is the
        // single deterministic choke-point where both the catalog root and the
        // freshly-built PipelineContext are in scope. LoadSkills still runs
        // later but the vocabulary slot will already be populated, so the
        // double-load is a no-op (LoadSkills sets the same vocabulary).
        var vocabulary = LoadVocabularyFromCatalog();
        pipeline.Set(ContextKeys.ConceptVocabulary, vocabulary);

        if (request.TicketId is not null)
            pipeline.Set(ContextKeys.TicketId, request.TicketId);

        if (request.IsInit)
        {
            pipeline.Set(ContextKeys.InitMode, true);
            pipeline.Set(ContextKeys.CheckoutBranch, "agentsmith/init");
        }

        if (request.Context is not null)
        {
            foreach (var (key, value) in request.Context)
                pipeline.Set(key, value);

            // Map ScanBranch to CheckoutBranch if not already set
            if (request.Context.ContainsKey(ContextKeys.ScanBranch)
                && !pipeline.Has(ContextKeys.CheckoutBranch))
            {
                pipeline.Set(ContextKeys.CheckoutBranch, request.Context[ContextKeys.ScanBranch]);
            }
        }

        // p0128b: operator answers from a prior open-questions round-trip flow into
        // the next Plan-skill run as a structured input block (PromptPrefixBuilder).
        if (request.PlanAnswers is { Count: > 0 })
            pipeline.Set(ContextKeys.PlanAnswers, request.PlanAnswers);

        sourceConfigOverrider.Apply(projectConfig, pipeline);

        var result = await pipelineExecutor.ExecuteAsync(
            commands, projectConfig, pipeline, cancellationToken);

        if (result.IsSuccess && pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var prUrl))
            result = result with { PrUrl = prUrl };

        LogResult(result, projectName, pipeline);
        return result;
    }

    /// <summary>
    /// Resolves the repos this run will operate on. By default returns all configured repos.
    /// If ContextKeys.SourceOverrideRepo is set in the request context (CLI `--repo NAME`),
    /// filters down to that single repo; unknown names throw with the known-repos list.
    /// </summary>
    private static IReadOnlyList<RepoConnection> ResolveRepos(
        ResolvedProject project, IReadOnlyDictionary<string, object>? requestContext)
    {
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos configured.");

        if (requestContext is null
            || !requestContext.TryGetValue(ContextKeys.SourceOverrideRepo, out var value)
            || value is not string repoName
            || string.IsNullOrEmpty(repoName))
            return project.Repos;

        var match = project.Repos.SingleOrDefault(r =>
            string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new InvalidOperationException(
                $"--repo '{repoName}' does not match any repo in project '{project.Name}'. "
                + $"Known repos: [{string.Join(", ", project.Repos.Select(r => r.Name))}].");
        return new[] { match };
    }

    /// <summary>
    /// Legacy entry point for backward compatibility with string-based input.
    /// Parses intent from free text, builds a PipelineRequest, and delegates.
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        string userInput,
        string configPath,
        bool headless,
        string? pipelineOverride,
        CancellationToken cancellationToken,
        Dictionary<string, object>? initialContext = null)
    {
        logger.LogInformation("Processing input: {Input}", userInput);

        var request = await BuildRequestFromLegacyInput(
            userInput, configPath, headless, pipelineOverride, initialContext, cancellationToken);

        return await ExecuteAsync(request, configPath, cancellationToken);
    }

    private async Task<PipelineRequest> BuildRequestFromLegacyInput(
        string userInput, string configPath, bool headless,
        string? pipelineOverride, Dictionary<string, object>? initialContext,
        CancellationToken cancellationToken)
    {
        var intent = await intentParser.ParseAsync(userInput, cancellationToken);
        var config = configLoader.LoadConfig(configPath);
        var projectName = intent.ProjectName.Value;
        var pipelineName = ResolvePipelineName(pipelineOverride, config, projectName, fallback: "fix-bug");

        return new PipelineRequest(
            projectName, pipelineName,
            TicketId: intent.TicketId,
            Headless: headless, Context: initialContext);
    }

    private string ResolvePipelineName(
        string? pipelineOverride, AgentSmithConfig config, string projectName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(pipelineOverride)) return pipelineOverride;
        if (!config.Projects.TryGetValue(projectName, out var project)) return fallback;
        try { return pipelineConfigResolver.ResolveDefaultPipelineName(project); }
        catch (InvalidOperationException) { return fallback; }
    }

    /// <summary>
    /// p0125c-followup: shared concept vocabulary is loaded from the catalog's
    /// <c>skills/</c> subtree. <see cref="ISkillsCatalogPath.Root"/> points at
    /// the extracted catalog root (e.g. <c>~/.cache/agentsmith/skills/</c>); the
    /// vocab YAML sits at <c>{Root}/skills/concept-vocabulary.yaml</c>.
    /// Returns <see cref="ConceptVocabulary.Empty"/> when the catalog isn't
    /// bootstrapped yet (CLI tooling running before the resolver wired it up,
    /// or dev-from-source with no catalog at all). The empty vocab matches the
    /// pre-fix behavior; concept-writers in early steps will throw with the
    /// same KeyNotFoundException as before, surfacing the missing-catalog
    /// problem clearly rather than masking it.
    /// </summary>
    private ConceptVocabulary LoadVocabularyFromCatalog()
    {
        try
        {
            var skillsRoot = Path.Combine(catalogPath.Root, CatalogSkillsRootSubPath);
            if (!Directory.Exists(skillsRoot))
            {
                logger.LogWarning(
                    "Skills root {Path} not present at pipeline-bootstrap; concept vocabulary " +
                    "will be empty until LoadSkills repopulates it.", skillsRoot);
                return ConceptVocabulary.Empty;
            }
            return skillLoader.LoadVocabulary(skillsRoot) ?? ConceptVocabulary.Empty;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Catalog not yet bootstrapped at pipeline-start; concept vocabulary " +
                "will be empty until LoadSkills repopulates it.");
            return ConceptVocabulary.Empty;
        }
    }

    private void LogResult(CommandResult result, string projectName, PipelineContext pipeline)
    {
        var cost = PipelineCostTracker.GetOrCreate(pipeline);
        if (result.IsSuccess)
            logger.LogInformation(
                "Project {Project} processed successfully: {Message} | {Cost}",
                projectName, result.Message, cost);
        else
            logger.LogWarning(
                "Project {Project} processing failed: {Message} | {Cost}",
                projectName, result.Message, cost);
    }
}
