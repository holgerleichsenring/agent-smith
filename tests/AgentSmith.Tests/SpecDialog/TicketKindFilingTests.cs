using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-18-b4f0: the FILING side of the work-item kind — the resolver that turns a role
/// into a kind, and the chat-filed ticket, which is the one call site that is not a
/// spec-dialog outcome.
/// </summary>
public sealed class TicketKindFilingTests
{
    /// <summary>
    /// The map's keys are free text: no capability field can declare a map's legal keys today,
    /// so a mistyped role saves cleanly and renders cleanly. It must not also file silently — a
    /// wrong role key files at the wrong hierarchy level and surfaces only as a failed parent
    /// link, which is LESS observable than the unknown lifecycle key that already warns.
    /// </summary>
    [Fact]
    public void Resolve_AnUnknownRoleKey_IsLoggedAndFallsBackToTheLiteral()
    {
        var logger = new CapturingLogger<TicketKindResolver>();
        var project = Project(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phaze"] = "Task",
        });

        var kind = new TicketKindResolver(logger).For(project, TicketFilingRole.Phase);

        kind.Should().BeNull("an unmapped role sends no kind, so the provider sends its literal");
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("phaze").And.Contain("work_item_kinds").And.Contain("phase");
    }

    /// <summary>
    /// 2026-09-22-b3d7: `record` went with the slice records it filed. A live configuration that
    /// still maps it keeps validating — the map's keys are free text — and is warned about as the
    /// unknown role it now is, rather than silently filing something at a level nothing files at.
    /// </summary>
    [Fact]
    public void Resolve_AStaleRecordRoleKey_IsWarnedAboutAsUnknown()
    {
        var logger = new CapturingLogger<TicketKindResolver>();
        var project = Project(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["work"] = "Feature",
            ["record"] = "Task",
        });

        new TicketKindResolver(logger).For(project, TicketFilingRole.Work)
            .Should().Be("Feature", "the roles that survive still resolve beside a stale key");
        logger.Warnings.Should().ContainSingle().Which.Should()
            .Contain("record").And.Contain("work_item_kinds")
            .And.NotContain("record,", "the known-role list it prints no longer offers it");
    }

    /// <summary>
    /// 2026-09-24-f962: this used to assert SILENCE — "an installation that configured nothing has
    /// nothing to report". It has the most to report. An unconfigured tracker files the provider's
    /// own literal, and a lifecycle status that literal's type does not have cannot be reached at
    /// the run's end, which blocks the ticket on every later claim. An operator hit that three
    /// times in a row with nothing on any surface naming the type.
    /// </summary>
    [Fact]
    public void Resolve_ATrackerThatConfiguresNothing_ResolvesNothingAndSaysSo()
    {
        var logger = new CapturingLogger<TicketKindResolver>();

        var kind = new TicketKindResolver(logger).For(Project(null), TicketFilingRole.Work);

        kind.Should().BeNull();
        string.Join("\n", logger.Lines).Should()
            .Contain("configures no work_item_kinds").And.Contain("default type");
        logger.Warnings.Should().BeEmpty("nothing is wrong — it is a default, not a mistake");
    }

    /// <summary>
    /// The fifth call site. A chat request files a ticket through the same create as every
    /// outcome does, and it is its own role: nothing about its content says what it is.
    /// </summary>
    [Fact]
    public async Task Create_AChatFiledTicket_SendsTheKindConfiguredForThatRole()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

        await Handler(provider.Object, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["chat"] = "Issue",
            ["work"] = "Feature",
        }).HandleAsync(Intent(), CancellationToken.None);

        provider.Verify(p => p.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
            "Issue", It.IsAny<CancellationToken>()));
    }

    private static CreateTicketIntentHandler Handler(
        ITicketProvider provider, IReadOnlyDictionary<string, string> kinds)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["proj"] = Project(kinds) },
        });
        return new CreateTicketIntentHandler(
            Mock.Of<IPlatformAdapter>(), loader.Object, factory.Object,
            new TicketKindResolver(NullLogger<TicketKindResolver>.Instance),
            NullLogger<CreateTicketIntentHandler>.Instance);
    }

    private static CreateTicketIntent Intent() => new()
    {
        RawText = "create", UserId = "U1", ChannelId = "C1", Platform = "slack",
        Project = "proj", Title = "The widget is lost",
    };

    private static ResolvedProject Project(IReadOnlyDictionary<string, string>? kinds) => new()
    {
        Name = "proj",
        Tracker = new TrackerConnection
        {
            Name = "sample-tracker",
            Type = TrackerType.AzureDevOps,
            WorkItemKinds = kinds ?? new Dictionary<string, string>(),
        },
    };

    /// <summary>
    /// 2026-09-24-f962: the unconfigured path is the one whose consequence arrives latest — the
    /// provider files its own literal, and a lifecycle status that type does not have blocks the
    /// ticket for good at the run's end. It must not be the silent path.
    /// </summary>
    [Fact]
    public void For_ATrackerThatConfiguresNoKinds_ReportsThatItFilesTheDefault()
    {
        var logger = new KindLogger();
        var project = ProjectWithKinds(new Dictionary<string, string>());

        new TicketKindResolver(logger).For(project, TicketFilingRole.Phase).Should().BeNull();

        string.Join("\n", logger.Lines).Should()
            .Contain("configures no work_item_kinds").And.Contain("default type");
    }

    [Fact]
    public void For_AConfiguredRole_ReportsTheTypeItChose()
    {
        var logger = new KindLogger();
        var project = ProjectWithKinds(new Dictionary<string, string> { ["phase"] = "User Story" });

        new TicketKindResolver(logger).For(project, TicketFilingRole.Phase).Should().Be("User Story");

        string.Join("\n", logger.Lines).Should().Contain("User Story");
    }

    private static ResolvedProject ProjectWithKinds(IReadOnlyDictionary<string, string> kinds) =>
        new()
        {
            Name = "p1",
            Tracker = new TrackerConnection { Name = "t1", WorkItemKinds = kinds },
        };

    private sealed class KindLogger : ILogger<TicketKindResolver>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
