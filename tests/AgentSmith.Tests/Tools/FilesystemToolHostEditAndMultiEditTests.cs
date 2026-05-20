using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class FilesystemToolHostEditAndMultiEditTests
{
    [Fact]
    public async Task Edit_ReplaceAllTrue_ReplacesEveryOccurrence_ReportsCount()
    {
        var (host, written) = BuildWithContent("foo bar foo baz foo");

        var result = await host.Edit("file.cs", "foo", "qux", replace_all: true);

        result.Should().Contain("Replaced 3 occurrence(s)");
        written.Last().Should().Be("qux bar qux baz qux");
    }

    [Fact]
    public async Task Edit_ReplaceAllFalse_RejectsMultipleOccurrences()
    {
        var (host, _) = BuildWithContent("foo bar foo");

        var result = await host.Edit("file.cs", "foo", "qux");

        result.Should().StartWith("Error").And.Contain("multiple times");
    }

    [Fact]
    public async Task Edit_SingleOccurrence_ReplacesOnce()
    {
        var (host, written) = BuildWithContent("the only foo here");

        var result = await host.Edit("file.cs", "foo", "qux");

        result.Should().StartWith("File written");
        written.Last().Should().Be("the only qux here");
    }

    [Fact]
    public async Task MultiEdit_AppliesAllEditsAtomically_InOrder()
    {
        var (host, written) = BuildWithContent("alpha beta gamma");
        var edits = new List<FilesystemToolHost.MultiEditOp>
        {
            new("alpha", "ONE"),
            new("beta", "TWO"),
            new("gamma", "THREE")
        };

        var result = await host.MultiEdit("file.cs", edits);

        result.Should().Contain("Applied 3 edit(s)");
        written.Last().Should().Be("ONE TWO THREE");
    }

    [Fact]
    public async Task MultiEdit_OneEditFails_NothingWritten()
    {
        var (host, written) = BuildWithContent("alpha beta");
        var edits = new List<FilesystemToolHost.MultiEditOp>
        {
            new("alpha", "ONE"),
            new("not-present", "X")
        };

        var result = await host.MultiEdit("file.cs", edits);

        result.Should().StartWith("Error in edit #2");
        written.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiEdit_DryRun_DoesNotWriteFile()
    {
        var (host, written) = BuildWithContent("alpha beta");
        var edits = new List<FilesystemToolHost.MultiEditOp> { new("alpha", "ONE") };

        var result = await host.MultiEdit("file.cs", edits, dry_run: true);

        result.Should().StartWith("dry_run").And.Contain("would apply 1 edit");
        written.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiEdit_EmptyEdits_ReturnsError()
    {
        var (host, _) = BuildWithContent("anything");
        var result = await host.MultiEdit("file.cs", new List<FilesystemToolHost.MultiEditOp>());
        result.Should().StartWith("Error").And.Contain("at least one");
    }

    private static (FilesystemToolHost Host, List<string> WrittenContents) BuildWithContent(string initialContent)
    {
        var written = new List<string>();
        var content = initialContent;
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.Is<Step>(st => st.Kind == StepKind.ReadFile),
                It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, content));
        sandbox.Setup(s => s.RunStepAsync(It.Is<Step>(st => st.Kind == StepKind.WriteFile),
                It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                written.Add(step.Content!);
                content = step.Content!;
            })
            .ReturnsAsync(() => new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "OK"));
        return (new FilesystemToolHost(sandbox.Object), written);
    }
}
