using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class WriteRunResultHandlerTests : IDisposable
{
    private readonly WriteRunResultHandler _sut;
    private readonly string _tempDir;

    public WriteRunResultHandlerTests()
    {
        _sut = new WriteRunResultHandler(NullLogger<WriteRunResultHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-runresult-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".agentsmith"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesRunDirectory()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        var runsDir = Path.Combine(_tempDir, ".agentsmith", "runs");
        Directory.Exists(runsDir).Should().BeTrue();
        Directory.GetDirectories(runsDir).Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WritesPlanFile()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        var runDir = Directory.GetDirectories(Path.Combine(_tempDir, ".agentsmith", "runs"))[0];
        var planFile = Path.Combine(runDir, "plan.md");
        File.Exists(planFile).Should().BeTrue();

        var content = File.ReadAllText(planFile);
        content.Should().Contain("Add login feature");
        content.Should().Contain("Test summary");
    }

    [Fact]
    public async Task ExecuteAsync_WritesResultFile()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        var runDir = Directory.GetDirectories(Path.Combine(_tempDir, ".agentsmith", "runs"))[0];
        var resultFile = Path.Combine(runDir, "result.md");
        File.Exists(resultFile).Should().BeTrue();

        var content = File.ReadAllText(resultFile);
        content.Should().Contain("Add login feature");
        content.Should().Contain("[Create] src/Login.cs");
    }

    [Fact]
    public async Task ExecuteAsync_AppendsToContextYaml()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        var yaml = File.ReadAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"));
        yaml.Should().Contain("r01:");
        yaml.Should().Contain("feat #42");
    }

    [Fact]
    public async Task ExecuteAsync_StoresRunNumberInPipeline()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        context.Pipeline.TryGet<int>(ContextKeys.RunNumber, out var runNumber).Should().BeTrue();
        runNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FixTicket_WritesFixType()
    {
        SetupContextYaml();
        var context = CreateContext("Fix null reference in checkout");

        await _sut.ExecuteAsync(context);

        var yaml = File.ReadAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"));
        yaml.Should().Contain("fix #42");
    }

    [Fact]
    public async Task ExecuteAsync_ExistingRuns_IncrementsNumber()
    {
        var yaml = "state:\n  done:\n    p01: \"initial setup\"\n    r01: \"feat #10: Add auth\"\n    r02: \"fix #11: Fix login\"\n  active: {}";
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), yaml);

        var context = CreateContext("Add dashboard");

        await _sut.ExecuteAsync(context);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"));
        content.Should().Contain("r03:");

        context.Pipeline.TryGet<int>(ContextKeys.RunNumber, out var runNumber).Should().BeTrue();
        runNumber.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_RunDirNameIncludesSlug()
    {
        SetupContextYaml();
        var context = CreateContext("Add login feature");

        await _sut.ExecuteAsync(context);

        var runDirs = Directory.GetDirectories(Path.Combine(_tempDir, ".agentsmith", "runs"));
        runDirs[0].Should().EndWith("r01-add-login-feature");
    }

    [Theory]
    [InlineData("No file", 1)]
    [InlineData("", 1)]
    public void DetermineNextRunNumber_NoExistingRuns_Returns1(string scenario, int expected)
    {
        var path = scenario == "No file"
            ? Path.Combine(_tempDir, "nonexistent.yaml")
            : Path.Combine(_tempDir, "empty.yaml");

        if (scenario == "")
            File.WriteAllText(path, "state:\n  done: {}\n  active: {}");

        var result = WriteRunResultHandler.DetermineNextRunNumber(path);
        result.Should().Be(expected);
    }

    [Fact]
    public void DetermineNextRunNumber_WithExistingRuns_ReturnsNext()
    {
        var path = Path.Combine(_tempDir, "test.yaml");
        File.WriteAllText(path, "state:\n  done:\n    r01: \"first\"\n    r02: \"second\"\n  active: {}");

        var result = WriteRunResultHandler.DetermineNextRunNumber(path);
        result.Should().Be(3);
    }

    [Fact]
    public void DetermineNextRunNumber_MixedPhaseAndRunKeys_OnlyCountsRuns()
    {
        var path = Path.Combine(_tempDir, "test.yaml");
        File.WriteAllText(path, "state:\n  done:\n    p01: \"init\"\n    p02: \"auth\"\n    r01: \"first run\"\n  active: {}");

        var result = WriteRunResultHandler.DetermineNextRunNumber(path);
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
        var yaml = "state:\n  done: {}\n  active: {}";
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), yaml);
    }

    private WriteRunResultContext CreateContext(string ticketTitle)
    {
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");
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
        return new WriteRunResultContext(repo, plan, ticket, changes, new PipelineContext());
    }
}
