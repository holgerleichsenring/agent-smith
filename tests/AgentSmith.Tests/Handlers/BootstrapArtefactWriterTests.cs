using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-15-d66f: the artefacts a delta declares are written PER PATH.
/// <para>
/// The case that matters most is the first one: a repository that already carries a ratified
/// principles.md must still receive an artefact it does not have. The round-level preserve
/// decision used to answer for the whole set, which suppressed every artefact in exactly the
/// established repositories the artefacts exist for.
/// </para>
/// </summary>
public sealed class BootstrapArtefactWriterTests
{
    private static readonly PrinciplesArtefact Editor =
        new(".editorconfig", "root = true\n");
    private static readonly PrinciplesArtefact Props =
        new("Directory.Build.props", "<Project />\n");

    private readonly Mock<ISandboxFileReader> _reader = new();
    private readonly Mock<ISandbox> _sandbox = new();
    private readonly BootstrapArtefactWriter _sut;

    public BootstrapArtefactWriterTests()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_reader.Object);
        _sandbox
            .Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(
                StepResult.CurrentSchemaVersion, Guid.NewGuid(), ExitCode: 0,
                TimedOut: false, DurationSeconds: 0, ErrorMessage: null));
        _sut = new BootstrapArtefactWriter(factory.Object, NullLogger<BootstrapArtefactWriter>.Instance);
    }

    [Fact]
    public async Task Transfer_ExistingPrinciplesAndAbsentArtefact_WritesTheArtefact()
    {
        Absent(Editor.Path);

        var writes = await ApplyAsync(Editor);

        writes.Should().ContainSingle().Which.Status.Should().Be(ArtefactStatus.Written);
        _sandbox.Verify(s => s.RunStepAsync(
            It.Is<Step>(step => step.Kind == StepKind.WriteFile && step.Path == Editor.Path),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transfer_ExistingArtefact_IsPreservedPerPath()
    {
        Present(Editor.Path);
        Absent(Props.Path);

        var writes = await ApplyAsync(Editor, Props);

        writes.Should().HaveCount(2);
        writes[0].Status.Should().Be(ArtefactStatus.PreservedExisting,
            "a file the repository already carries is the operator's");
        writes[1].Status.Should().Be(ArtefactStatus.Written,
            "one preserved path must not suppress the next — that is the whole reason "
            + "preserve-existing moved from the round to the path");
    }

    [Fact]
    public async Task Transfer_ArtefactPathEscapesWorkRoot_FailsTheRound()
    {
        var escaping = new PrinciplesArtefact("../outside/.editorconfig", "x\n");

        var writes = await ApplyAsync(escaping);

        var write = writes.Should().ContainSingle().Subject;
        write.Status.Should().Be(ArtefactStatus.Refused);
        write.Reason.Should().NotBeNullOrWhiteSpace("a refusal that does not say why is a shrug");
        _sandbox.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Never, "a path outside the work root is never written, not even partly");
    }

    [Fact]
    public async Task Transfer_AbsolutePath_IsRefusedToo()
    {
        var rooted = new PrinciplesArtefact("/etc/profile", "x\n");

        var writes = await ApplyAsync(rooted);

        writes.Should().ContainSingle().Which.Status.Should().Be(ArtefactStatus.Refused);
    }

    [Fact]
    public async Task Transfer_SecondComponentOfOneRepo_ReportsAlreadyWrittenNotRatified()
    {
        // One repository fans out one round per component against ONE checkout, so the file
        // the first component wrote is present for the second. Calling that "ratified" would
        // attribute this run's own write to the operator.
        Present(Editor.Path);

        var writes = await ApplyAsync([Editor.Path], Editor);

        writes.Should().ContainSingle().Which.Status.Should().Be(ArtefactStatus.AlreadyWrittenThisRound);
    }

    [Fact]
    public async Task Transfer_NoArtefactsDeclared_TouchesNothing()
    {
        var writes = await ApplyAsync();

        writes.Should().BeEmpty();
        _reader.Verify(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void Present(string path) => _reader
        .Setup(r => r.ExistsAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private void Absent(string path) => _reader
        .Setup(r => r.ExistsAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(false);

    private Task<IReadOnlyList<ArtefactWrite>> ApplyAsync(params PrinciplesArtefact[] artefacts) =>
        ApplyAsync([], artefacts);

    private Task<IReadOnlyList<ArtefactWrite>> ApplyAsync(
        string[] writtenEarlier, params PrinciplesArtefact[] artefacts) =>
        _sut.ApplyAsync(
            _sandbox.Object, "repo", "default", artefacts,
            writtenEarlier.ToHashSet(StringComparer.Ordinal), CancellationToken.None);
}
