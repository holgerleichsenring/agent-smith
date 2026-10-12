using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Repositories;

/// <summary>
/// 2026-10-12-f5f7c: a run's sandbox map as the coordinator publishes it — sandbox key, owning
/// repository, a file surface per sandbox and the working tree <c>git status</c> reports.
/// Every <see cref="Add"/> republishes both maps, as ensure_repo_sandbox does mid-run.
/// </summary>
internal sealed class RunRepositoriesTestRun
{
    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
    private readonly Dictionary<ISandbox, InMemorySandboxFileReader> _readers = new();

    public PipelineContext Pipeline { get; } = new();

    public InMemorySandboxFileReader Add(string key, string repository, string porcelain = "")
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(
                new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, porcelain)));
        var reader = new InMemorySandboxFileReader();
        _readers[sandbox.Object] = reader;
        _sandboxes[key] = sandbox.Object;
        _owners[key] = repository;
        Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(_sandboxes, StringComparer.Ordinal));
        Pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, new Dictionary<string, string>(_owners, StringComparer.Ordinal));
        return reader;
    }

    public RunRepositoriesFactory Factory()
    {
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns<ISandbox>(s => _readers[s]);
        return new RunRepositoriesFactory(new SandboxTargets(), readers.Object,
            new SandboxWorkingTreeReader(NullLogger<SandboxWorkingTreeReader>.Instance));
    }

    public IRunRepositories Repositories() => Factory().For(Pipeline);
}
