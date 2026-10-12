using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Repositories;

/// <summary>2026-10-12-f5f7c: builds a pipeline's <see cref="IRunRepositories"/>.</summary>
public sealed class RunRepositoriesFactory(
    SandboxTargets sandboxTargets,
    ISandboxFileReaderFactory readerFactory,
    SandboxWorkingTreeReader workingTree)
{
    public IRunRepositories For(PipelineContext pipeline) =>
        new PipelineRunRepositories(pipeline, sandboxTargets, readerFactory, workingTree);
}
