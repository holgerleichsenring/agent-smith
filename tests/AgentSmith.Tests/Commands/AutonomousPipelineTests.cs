using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class AutonomousPipelineTests : IDisposable
{
    private readonly string _tempDir;

    public AutonomousPipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-auto-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Pipeline Preset

    [Fact]
    public void Autonomous_PresetResolves()
    {
        var preset = PipelinePresets.TryResolve("autonomous");

        preset.Should().NotBeNull();
        preset!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Autonomous_PresetContainsExpectedCommands()
    {
        PipelinePresets.Autonomous.Should().Contain(CommandNames.CheckoutSource);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.LoadRuns);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.WriteTickets);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.WriteRunResult);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.Triage);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.ConvergenceCheck);
        PipelinePresets.Autonomous.Should().Contain(CommandNames.CompileDiscussion);
    }

    [Fact]
    public void Autonomous_DefaultSkillsPath_IsCoding()
    {
        PipelinePresets.GetDefaultSkillsPath("autonomous").Should().Be("skills/coding");
    }

    #endregion

    #region AutonomousConfig Defaults

    [Fact]
    public void AutonomousConfig_HasCorrectDefaults()
    {
        var config = new AutonomousConfig();

        config.MaxTickets.Should().Be(3);
        config.MinConfidence.Should().Be(7);
        config.LookbackRuns.Should().Be(10);
        config.Roles.Should().Be("auto");
    }

    #endregion

    #region LoadRunsHandler

    [Fact]
    public async Task LoadRunsHandler_ReadsNMostRecentRuns()
    {
        SetupRuns("r01-first", "r02-second", "r03-third", "r04-fourth", "r05-fifth");

        var handler = new LoadRunsHandler(NullLogger<LoadRunsHandler>.Instance);
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), string.Empty);
        var context = new LoadRunsContext(repo, LookbackRuns: 3, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("3 recent run(s)");
        pipeline.TryGet<string>(ContextKeys.RunHistory, out var history).Should().BeTrue();
        history.Should().Contain("r03");
        history.Should().Contain("r04");
        history.Should().Contain("r05");
        history.Should().NotContain("r01");
        history.Should().NotContain("r02");
    }

    [Fact]
    public async Task LoadRunsHandler_NoRuns_ReturnsOk()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".agentsmith"));

        var handler = new LoadRunsHandler(NullLogger<LoadRunsHandler>.Instance);
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), string.Empty);
        var context = new LoadRunsContext(repo, LookbackRuns: 10, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No run history");
    }

    [Fact]
    public async Task LoadRunsHandler_IncludesWikiSummaries()
    {
        SetupRuns("r01-first");
        var wikiDir = Path.Combine(_tempDir, ".agentsmith", "wiki");
        Directory.CreateDirectory(wikiDir);
        await File.WriteAllTextAsync(Path.Combine(wikiDir, "patterns.md"), "# Patterns\nUse CQRS");

        var handler = new LoadRunsHandler(NullLogger<LoadRunsHandler>.Instance);
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), string.Empty);
        var context = new LoadRunsContext(repo, LookbackRuns: 10, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.RunHistory, out var history).Should().BeTrue();
        history.Should().Contain("Use CQRS");
    }

    #endregion

    #region WriteTicketsHandler

    [Fact]
    public async Task WriteTicketsHandler_RespectsMaxTickets()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider
            .Setup(x => x.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(x => x.Create(It.IsAny<TicketConfig>())).Returns(ticketProvider.Object);

        var handler = new WriteTicketsHandler(factory.Object, NullLogger<WriteTicketsHandler>.Instance);
        var pipeline = new PipelineContext();

        var findings = new List<AutonomousFinding>
        {
            new("F1", "Desc1", "architecture", 10, "architect", ["reviewer"], null),
            new("F2", "Desc2", "code-quality", 9, "reviewer", ["architect"], null),
            new("F3", "Desc3", "security", 8, "security", ["architect"], null),
        };
        pipeline.Set(ContextKeys.AutonomousFindings, (IReadOnlyList<AutonomousFinding>)findings);

        var context = new WriteTicketsContext(new TicketConfig(), MaxTickets: 2, MinConfidence: 1, pipeline);
        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Created 2 ticket(s)");

        ticketProvider.Verify(
            x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WriteTicketsHandler_RespectsMinConfidence()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(x => x.Create(It.IsAny<TicketConfig>())).Returns(ticketProvider.Object);

        var handler = new WriteTicketsHandler(factory.Object, NullLogger<WriteTicketsHandler>.Instance);
        var pipeline = new PipelineContext();

        var findings = new List<AutonomousFinding>
        {
            new("Low1", "Desc", "code-quality", 3, "reviewer", [], null),
            new("Low2", "Desc", "code-quality", 5, "reviewer", [], null),
        };
        pipeline.Set(ContextKeys.AutonomousFindings, (IReadOnlyList<AutonomousFinding>)findings);

        var context = new WriteTicketsContext(new TicketConfig(), MaxTickets: 10, MinConfidence: 7, pipeline);
        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("below minimum confidence");

        ticketProvider.Verify(
            x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WriteTicketsHandler_LabelsTicketsCorrectly()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider
            .Setup(x => x.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(x => x.Create(It.IsAny<TicketConfig>())).Returns(ticketProvider.Object);

        var handler = new WriteTicketsHandler(factory.Object, NullLogger<WriteTicketsHandler>.Instance);
        var pipeline = new PipelineContext();

        var findings = new List<AutonomousFinding>
        {
            new("Important", "Description", "architecture", 9, "architect", ["reviewer"], null),
        };
        pipeline.Set(ContextKeys.AutonomousFindings, (IReadOnlyList<AutonomousFinding>)findings);

        var context = new WriteTicketsContext(new TicketConfig(), MaxTickets: 5, MinConfidence: 1, pipeline);
        await handler.ExecuteAsync(context, CancellationToken.None);

        ticketProvider.Verify(x => x.CreateAsync(
            "Important",
            It.IsAny<string>(),
            It.Is<IReadOnlyList<string>>(labels => labels.Contains("agent-smith-autonomous")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteTicketsHandler_NoFindings_ReturnsOk()
    {
        var factory = new Mock<ITicketProviderFactory>();
        var handler = new WriteTicketsHandler(factory.Object, NullLogger<WriteTicketsHandler>.Instance);
        var pipeline = new PipelineContext();

        var context = new WriteTicketsContext(new TicketConfig(), MaxTickets: 5, MinConfidence: 7, pipeline);
        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No findings");
    }

    [Fact]
    public void BuildTicketBody_IncludesAllMetadata()
    {
        var finding = new AutonomousFinding(
            "Test Finding", "A test description", "security", 8,
            "security-expert", ["architect", "reviewer"], null);

        var body = WriteTicketsHandler.BuildTicketBody(finding);

        body.Should().Contain("A test description");
        body.Should().Contain("**Category:** security");
        body.Should().Contain("**Confidence:** 8/10");
        body.Should().Contain("**Found by:** security-expert");
        body.Should().Contain("architect, reviewer");
    }

    #endregion

    #region CommandNames

    [Fact]
    public void CommandNames_HasAutonomousCommands()
    {
        CommandNames.LoadRuns.Should().Be("LoadRunsCommand");
        CommandNames.WriteTickets.Should().Be("WriteTicketsCommand");
    }

    [Fact]
    public void CommandNames_HasLabelsForAutonomousCommands()
    {
        CommandNames.GetLabel(CommandNames.LoadRuns).Should().Be("Loading run history");
        CommandNames.GetLabel(CommandNames.WriteTickets).Should().Be("Writing tickets");
    }

    #endregion

    private void SetupRuns(params string[] runNames)
    {
        var runsDir = Path.Combine(_tempDir, ".agentsmith", "runs");
        Directory.CreateDirectory(runsDir);

        foreach (var name in runNames)
        {
            var runDir = Path.Combine(runsDir, name);
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "result.md"), $"# Result for {name}\nTest result content");
        }
    }
}
