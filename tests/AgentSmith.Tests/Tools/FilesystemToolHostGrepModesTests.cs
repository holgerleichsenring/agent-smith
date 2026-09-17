using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class FilesystemToolHostGrepModesTests
{
    // 2026-09-17-042ed: grep_in_tree skips a fixed directory list, a size ceiling AND the
    // repository's ignore rules. A model that reads "no matches" as "not in the checkout" is
    // wrong about all three, and the only place it can learn otherwise is this description.
    [Fact]
    public void GrepInTree_Description_NamesWhatItDoesNotSearch()
    {
        var description = typeof(FilesystemToolHost).GetMethod(nameof(FilesystemToolHost.GrepInTree))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), inherit: false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        foreach (var skipped in GrepScope.ExcludedDirs) description.Should().Contain(skipped);
        description.Should().Contain("1 MB").And.Contain("ignore rules");
        GrepScope.MaxFileSizeBytes.Should().Be(1_000_000, "the description says 1 MB in words");
    }

    // The step the tool sends keeps the repository's ignore rules: only the proposal review's
    // own look asks for hidden and ignored paths.
    [Fact]
    public async Task GrepInTree_DoesNotAskForHiddenOrIgnoredPaths()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[]"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.GrepInTree("foo", ".");
        await host.GrepInFile("a.cs", "foo");

        captured!.SearchHidden.Should().BeFalse();
    }

    [Fact]
    public async Task GrepInTree_DefaultsToContentMode()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[]"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.GrepInTree("foo", ".");

        captured!.OutputMode.Should().Be(GrepOutputMode.Content);
    }

    [Fact]
    public async Task GrepInTree_OutputModeFilesWithMatches_RendersPathsOnly()
    {
        var sandbox = MockSandboxWithGrepResult("[{\"path\":\"a.cs\"},{\"path\":\"b.cs\"}]");
        var host = new FilesystemToolHost(sandbox.Object);

        var result = await host.GrepInTree("foo", ".", output_mode: "files_with_matches");

        result.Should().Contain("a.cs").And.Contain("b.cs").And.NotContain(":");
    }

    [Fact]
    public async Task GrepInTree_OutputModeCount_RendersPathColonCount()
    {
        var sandbox = MockSandboxWithGrepResult("[{\"path\":\"a.cs\",\"count\":5},{\"path\":\"b.cs\",\"count\":2}]");
        var host = new FilesystemToolHost(sandbox.Object);

        var result = await host.GrepInTree("foo", ".", output_mode: "count");

        result.Should().Contain("a.cs: 5").And.Contain("b.cs: 2");
    }

    [Fact]
    public async Task GrepInTree_ContextShorthand_AppliesToBothBeforeAndAfter()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[]"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.GrepInTree("foo", ".", context: 3);

        captured!.ContextBefore.Should().Be(3);
        captured.ContextAfter.Should().Be(3);
    }

    [Fact]
    public async Task GrepInTree_PassesHeadLimitToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[]"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.GrepInTree("foo", ".", head_limit: 50);

        captured!.HeadLimit.Should().Be(50);
    }

    private static Mock<ISandbox> MockSandboxWithGrepResult(string outputContent)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, outputContent));
        return sandbox;
    }
}
