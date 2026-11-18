using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-f3a8a: context for one admin assistant turn — the installation project and the run's seeds.</summary>
public sealed record AdminAssistantTurnContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
