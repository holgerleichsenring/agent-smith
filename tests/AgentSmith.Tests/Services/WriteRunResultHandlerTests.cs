using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class WriteRunResultHandlerTests
{
    private readonly InMemoryDialogueTrail _dialogueTrail = new();
    private readonly Dictionary<string, string> _written = new();
    private readonly Dictionary<string, string?> _initialFiles = new();
    private readonly WriteRunResultHandler _sut;

    public WriteRunResultHandlerTests()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((p, _) =>
            {
                if (_written.TryGetValue(p, out var c)) return Task.FromResult<string?>(c);
                if (_initialFiles.TryGetValue(p, out var i)) return Task.FromResult(i);
                return Task.FromResult<string?>(null);
            });
        reader.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, _) => _written[p] = c)
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);

        _sut = new WriteRunResultHandler(
            factory.Object, _dialogueTrail, NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WritesPlanFile()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var planEntry = _written.FirstOrDefault(kv => kv.Key.EndsWith("plan.md"));
        planEntry.Key.Should().NotBeNull();
        planEntry.Value.Should().Contain("Add login feature");
        planEntry.Value.Should().Contain("Test summary");
    }

    [Fact]
    public async Task ExecuteAsync_WritesResultWithFrontmatter()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var resultEntry = _written.FirstOrDefault(kv => kv.Key.EndsWith("result.md"));
        resultEntry.Key.Should().NotBeNull();
        var content = resultEntry.Value;

        content.Should().Contain("---");
        content.Should().Contain("ticket: \"#42");
        content.Should().Contain("result: success");
        content.Should().Contain("type: feat");
        content.Should().Contain("[Create] src/Login.cs");
    }

    [Fact]
    public async Task ExecuteAsync_WithCostData_IncludesCostInFrontmatter()
    {
        SetupContextYaml();
        var pipeline = NewPipelineWithSandbox();
        var phases = new Dictionary<string, PhaseCost>
        {
            ["scout"] = new("claude-haiku-4-5-20251001", 4200, 1800, 2100, 1, 0.02m),
            ["primary"] = new("claude-sonnet-4-20250514", 38150, 6320, 16100, 6, 0.36m)
        };
        pipeline.Set(ContextKeys.RunCostSummary, new RunCostSummary(phases.AsReadOnly(), 0.38m));
        pipeline.Set(ContextKeys.RunDurationSeconds, 145);

        var context = CreateContext("Add login feature", pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var resultEntry = _written.First(kv => kv.Key.EndsWith("result.md"));
        var content = resultEntry.Value;

        content.Should().Contain("duration_seconds: 145");
        content.Should().Contain("tokens:");
        content.Should().Contain("  input: 42350");
        content.Should().Contain("  output: 8120");
        content.Should().Contain("total_usd: 0.3800");
        content.Should().Contain("    scout:");
        content.Should().Contain("    primary:");
        content.Should().Contain("      model: claude-sonnet-4-20250514");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCostData_OmitsCostSections()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var resultEntry = _written.First(kv => kv.Key.EndsWith("result.md"));
        var content = resultEntry.Value;

        content.Should().Contain("---");
        content.Should().Contain("ticket:");
        content.Should().NotContain("tokens:");
        content.Should().NotContain("cost:");
        content.Should().NotContain("duration_seconds:");
    }

    [Fact]
    public void BuildFrontmatter_UsesInvariantCulture()
    {
        var phases = new Dictionary<string, PhaseCost>
        {
            ["primary"] = new("model", 1000, 500, 200, 3, 0.1234m)
        };
        var costSummary = new RunCostSummary(phases.AsReadOnly(), 0.1234m);
        var ticket = new Ticket(new TicketId("1"), "Test", "Desc", null, "Open", "github");

        var sb = new StringBuilder();
        RunCostSectionWriter.AppendFrontmatter(sb, ticket, "feat", 60, costSummary);
        var result = sb.ToString();

        result.Should().Contain("0.1234");
        result.Should().NotContain("0,1234");
    }

    [Fact]
    public async Task ExecuteAsync_AppendsToContextYaml()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var yaml = _written["/work/.agentsmith/context.yaml"];
        yaml.Should().Contain("r01:");
        yaml.Should().Contain("feat #42");
    }

    [Fact]
    public async Task ExecuteAsync_StoresRunNumberInPipeline()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<int>(ContextKeys.RunNumber, out var runNumber).Should().BeTrue();
        runNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FixTicket_WritesFixType()
    {
        SetupContextYaml();
        var context = CreateContext("Fix null reference in checkout");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var yaml = _written["/work/.agentsmith/context.yaml"];
        yaml.Should().Contain("fix #42");
    }

    [Fact]
    public async Task ExecuteAsync_WithDecisionsInPipeline_IncludesDecisionsInResult()
    {
        SetupContextYaml();
        var pipeline = NewPipelineWithSandbox();
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**Redis Streams**: fan-out required"),
            new("Tooling", "**DuckDB**: reads Parquet natively")
        };
        pipeline.Set(ContextKeys.Decisions, decisions);

        var context = CreateContext("Add caching layer", pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var content = _written.First(kv => kv.Key.EndsWith("result.md")).Value;

        content.Should().Contain("## Decisions");
        content.Should().Contain("### Architecture");
        content.Should().Contain("- **Redis Streams**: fan-out required");
        content.Should().Contain("### Tooling");
        content.Should().Contain("- **DuckDB**: reads Parquet natively");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDecisions_OmitsDecisionsSection()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var content = _written.First(kv => kv.Key.EndsWith("result.md")).Value;

        content.Should().NotContain("## Decisions");
    }

    [Fact]
    public async Task ExecuteAsync_ExistingRuns_IncrementsNumber()
    {
        var yaml = "state:\n  done:\n    p01: \"initial setup\"\n    r01: \"feat #10: Add auth\"\n    r02: \"fix #11: Fix login\"\n  active: {}";
        _initialFiles["/work/.agentsmith/context.yaml"] = yaml;

        var context = CreateContext("Add dashboard");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _written.Should().ContainKey("/work/.agentsmith/context.yaml");
        _written["/work/.agentsmith/context.yaml"].Should().Contain("r03:");

        context.Pipeline.TryGet<int>(ContextKeys.RunNumber, out var runNumber).Should().BeTrue();
        runNumber.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RunDirNameIncludesSlug()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var planPath = _written.Keys.First(k => k.EndsWith("plan.md"));
        planPath.Should().Contain("r01-add-login-feature");
    }

    [Fact]
    public void AppendDecisions_GroupsByCategory()
    {
        var sb = new StringBuilder();
        var decisions = new List<PlanDecision>
        {
            new("Architecture", "**First**: reason"),
            new("Tooling", "**Tool1**: reason"),
            new("Architecture", "**Second**: reason")
        };

        RunResultSectionWriter.AppendDecisions(sb, decisions);
        var result = sb.ToString();

        result.Should().Contain("## Decisions");
        result.Should().Contain("### Architecture");
        result.Should().Contain("### Tooling");
        result.Should().Contain("- **First**: reason");
        result.Should().Contain("- **Second**: reason");
        result.Should().Contain("- **Tool1**: reason");

        var archIndex = result.IndexOf("### Architecture", StringComparison.Ordinal);
        var toolIndex = result.IndexOf("### Tooling", StringComparison.Ordinal);
        archIndex.Should().BeLessThan(toolIndex);
    }

    [Fact]
    public void AppendDecisions_NullOrEmpty_WritesNothing()
    {
        var sb = new StringBuilder();
        RunResultSectionWriter.AppendDecisions(sb, null);
        sb.ToString().Should().BeEmpty();

        sb.Clear();
        RunResultSectionWriter.AppendDecisions(sb, new List<PlanDecision>());
        sb.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task DetermineNextRunNumberAsync_NoFile_Returns1()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await WriteRunResultHandler.DetermineNextRunNumberAsync(
            reader.Object, "/work/.agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be(1);
    }

    [Fact]
    public async Task DetermineNextRunNumberAsync_EmptyState_Returns1()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("state:\n  done: {}\n  active: {}");

        var result = await WriteRunResultHandler.DetermineNextRunNumberAsync(
            reader.Object, "/work/.agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be(1);
    }

    [Fact]
    public async Task DetermineNextRunNumberAsync_WithExistingRuns_ReturnsNext()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("state:\n  done:\n    r01: \"first\"\n    r02: \"second\"\n  active: {}");

        var result = await WriteRunResultHandler.DetermineNextRunNumberAsync(
            reader.Object, "/work/.agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be(3);
    }

    [Fact]
    public async Task DetermineNextRunNumberAsync_MixedPhaseAndRunKeys_OnlyCountsRuns()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("state:\n  done:\n    p01: \"init\"\n    p02: \"auth\"\n    r01: \"first run\"\n  active: {}");

        var result = await WriteRunResultHandler.DetermineNextRunNumberAsync(
            reader.Object, "/work/.agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be(2);
    }

    [Theory]
    [InlineData("Add login feature", "add-login-feature")]
    [InlineData("Fix: null reference!", "fix-null-reference")]
    [InlineData("UPPER CASE title", "upper-case-title")]
    public void GenerateSlug_ConvertsTitle(string title, string expected)
    {
        WriteRunResultHandler.GenerateSlug(title).Should().Be(expected);
    }

    [Fact]
    public void GenerateSlug_TruncatesLongTitle()
    {
        var longTitle = "This is a very long title that exceeds the forty character limit for slugs";
        var slug = WriteRunResultHandler.GenerateSlug(longTitle);

        slug.Length.Should().BeLessThanOrEqualTo(40);
        slug.Should().NotEndWith("-");
    }

    private void SetupContextYaml()
    {
        _initialFiles["/work/.agentsmith/context.yaml"] = "state:\n  done: {}\n  active: {}";
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return pipeline;
    }

    private WriteRunResultContext CreateContext(string ticketTitle, PipelineContext? pipeline = null)
    {
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
        var ticket = new Ticket(new TicketId("42"), ticketTitle, "Description", null, "Open", "github");
        var steps = new List<PlanStep>
        {
            new(1, "Create login component", new FilePath("src/Login.cs"), "Create")
        };
        var plan = new Plan("Test summary", steps, "{}");
        var changes = new List<CodeChange>
        {
            new(new FilePath("src/Login.cs"), "public class Login {}", "Create")
        };
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline ?? NewPipelineWithSandbox());
    }
}
