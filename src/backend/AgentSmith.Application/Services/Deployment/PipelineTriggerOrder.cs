using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863e: one pipeline to start — the sentence that heads the outcome, the repository the pipeline is
/// defined in, what to start, and how the started run is read back.</summary>
public sealed record PipelineTriggerOrder(string Headline, RepoConnection Repo, PipelineRunOrder Run, CiReadRequest Ci);
