using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0167a: AnalyzePrDiffHandler parses the provider's raw per-file patches into
/// ContextKeys.PrDiff. Uses the real UnifiedDiffParser (the parse IS the unit
/// under test); only the platform provider is mocked.
/// </summary>
public sealed class AnalyzePrDiffHandlerTests
{
    private static readonly RepoConnection Repo = new()
    {
        Name = "primary",
        Type = RepoType.GitHub,
        Url = "https://github.com/org/my-api",
    };

    private const string Patch = """
        @@ -1,4 +1,5 @@
         using System;
        -var x = 1;
        +var x = 2;
        +var y = 3;
         Console.WriteLine(x);
         // end
        """;

    private static (AnalyzePrDiffHandler Handler, AnalyzePrDiffContext Context) CreateSut(
        params ChangedFile[] files)
    {
        var provider = new Mock<IPrDiffProvider>();
        provider.Setup(p => p.GetDiffAsync("42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrDiff("basesha456", "headsha123", files));
        var factory = new Mock<IPrDiffProviderFactory>();
        factory.Setup(f => f.Create(Repo)).Returns(provider.Object);

        var handler = new AnalyzePrDiffHandler(
            factory.Object, new UnifiedDiffParser(), NullLogger<AnalyzePrDiffHandler>.Instance);
        return (handler, new AnalyzePrDiffContext(Repo, "42", new PipelineContext()));
    }

    [Fact]
    public async Task AnalyzePrDiff_UnifiedDiffWithAddedAndRemovedLines_ParsesPerFileHunks()
    {
        var (handler, context) = CreateSut(new ChangedFile("src/Program.cs", Patch, ChangeKind.Modified));

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var analysis = context.Pipeline.Get<PrDiffAnalysis>(ContextKeys.PrDiff);
        analysis.BaseSha.Should().Be("basesha456");
        analysis.HeadSha.Should().Be("headsha123");

        var file = analysis.Files.Should().ContainSingle().Subject;
        file.Path.Should().Be("src/Program.cs");
        file.Kind.Should().Be(PrFileChangeKind.Modified);
        file.IsBinary.Should().BeFalse();

        var hunk = file.Hunks.Should().ContainSingle().Subject;
        hunk.Should().BeEquivalentTo(new { OldStart = 1, OldCount = 4, NewStart = 1, NewCount = 5 });
        hunk.Lines.Should().BeEquivalentTo(new[]
        {
            new PrDiffLine(PrDiffLineKind.Context, 1, 1, "using System;"),
            new PrDiffLine(PrDiffLineKind.Removed, 2, null, "var x = 1;"),
            new PrDiffLine(PrDiffLineKind.Added, null, 2, "var x = 2;"),
            new PrDiffLine(PrDiffLineKind.Added, null, 3, "var y = 3;"),
            new PrDiffLine(PrDiffLineKind.Context, 3, 4, "Console.WriteLine(x);"),
            new PrDiffLine(PrDiffLineKind.Context, 4, 5, "// end"),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task AnalyzePrDiff_MultiHunkPatch_ResolvesLineNumbersPerHunkHeader()
    {
        const string patch = """
            @@ -3,2 +3,2 @@
             context-a
            -old-line
            +new-line
            @@ -20,2 +20,3 @@
             context-b
            +late-addition
             context-c
            """;
        var (handler, context) = CreateSut(new ChangedFile("a.txt", patch, ChangeKind.Modified));

        await handler.ExecuteAsync(context, CancellationToken.None);

        var hunks = context.Pipeline.Get<PrDiffAnalysis>(ContextKeys.PrDiff).Files[0].Hunks;
        hunks.Should().HaveCount(2);
        hunks[1].Lines.Should().BeEquivalentTo(new[]
        {
            new PrDiffLine(PrDiffLineKind.Context, 20, 20, "context-b"),
            new PrDiffLine(PrDiffLineKind.Added, null, 21, "late-addition"),
            new PrDiffLine(PrDiffLineKind.Context, 21, 22, "context-c"),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task AnalyzePrDiff_BinaryFileDiff_SkipsWithMetadataOnly()
    {
        var (handler, context) = CreateSut(
            new ChangedFile("assets/logo.png", "", ChangeKind.Added),
            new ChangedFile("src/Program.cs", Patch, ChangeKind.Modified));

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var analysis = context.Pipeline.Get<PrDiffAnalysis>(ContextKeys.PrDiff);
        var binary = analysis.Files.Single(f => f.Path == "assets/logo.png");
        binary.IsBinary.Should().BeTrue();
        binary.Kind.Should().Be(PrFileChangeKind.Added);
        binary.Hunks.Should().BeEmpty();
        analysis.Files.Single(f => f.Path == "src/Program.cs").Hunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzePrDiff_PublishesAuthoritativeHeadAndBaseShas()
    {
        var (handler, context) = CreateSut();
        // The webhook seeded provisional values; the platform's answer wins.
        context.Pipeline.Set(ContextKeys.PrHead, "stale-head");
        context.Pipeline.Set(ContextKeys.PrBase, "stale-base");

        await handler.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.Get<string>(ContextKeys.PrHead).Should().Be("headsha123");
        context.Pipeline.Get<string>(ContextKeys.PrBase).Should().Be("basesha456");
    }
}
