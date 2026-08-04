using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0179b: context for the AgenticMaster step that runs a master skill body
/// (resolved through IPromptCatalog → SkillCatalogPromptCatalog) in a single
/// agentic loop. Replaces the legacy Triage→Plan→…→AgenticExecute choreography
/// for coding pipelines. The master decides plan + execute + verify internally
/// — no choreography handlers.
/// </summary>
public sealed record AgenticMasterContext(
    string MasterSkillName,
    Repository Repository,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    IReadOnlyDictionary<string, string>? RepoCodeMaps = null,
    string? ProjectContext = null) : ICommandContext;
