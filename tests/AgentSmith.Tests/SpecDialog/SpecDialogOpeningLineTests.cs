using AgentSmith.Tests.TestSupport;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-28-1da5b: opening a conversation is announced on CHAT and not on the dashboard.
/// <para>
/// There a command opens the dialog, nothing renders a scope beside the thread, and the line is
/// the only acknowledgement that anything happened. On the dashboard the first MESSAGE is what
/// opens the conversation — so the announcement arrived after the operator's description, above
/// the turn already answering it, telling them to describe what they want to build.
/// </para>
/// <para>
/// The SEND is skipped rather than the text emptied. An empty push still travels composer,
/// messenger, adapter and hub, and the handler that absorbs it on the page also resets the turn's
/// working state and flips the flag deciding whether a proposal card gets a turn of its own.
/// </para>
/// </summary>
public sealed class SpecDialogOpeningLineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public SpecDialogOpeningLineTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    [Fact]
    public async Task SpecDialogOpened_OnTheDashboard_SaysNothing()
    {
        var adapter = Adapter(DispatcherDefaults.PlatformDashboard);

        await Handler(adapter).HandleAsync(
            new SpecOpenCommand("sample"), "u-1", "c-1", "d-1",
            DispatcherDefaults.PlatformDashboard, CancellationToken.None);

        adapter.Verify(a => a.SendInfoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SpecDialogOpened_OnChat_StillAnnouncesTheDialogAndItsScope()
    {
        var sent = new List<string>();
        var adapter = Adapter("slack", sent);

        await Handler(adapter).HandleAsync(
            new SpecOpenCommand("sample"), "u-1", "c-1", "t-1", "slack", CancellationToken.None);

        sent.Should().ContainSingle().Which.Should().Contain("opened").And.Contain("sample");
    }

    [Fact]
    public async Task SpecDialogOpened_OnTheDashboard_StillOpensTheConversation()
    {
        // Skipping the ANNOUNCEMENT must not skip the opening: the session is what the first
        // message is routed into.
        var adapter = Adapter(DispatcherDefaults.PlatformDashboard);

        await Handler(adapter).HandleAsync(
            new SpecOpenCommand("sample"), "u-1", "c-1", "d-1",
            DispatcherDefaults.PlatformDashboard, CancellationToken.None);

        (await new SpecDialogSessionRepository(_context).GetOpenByThreadAsync(
            DispatcherDefaults.PlatformDashboard, "d-1", CancellationToken.None))
            .Should().NotBeNull();
    }

    private SpecDialogCommandHandler Handler(Mock<IPlatformAdapter> adapter) =>
        new(new SpecDialogSessionManager(
                new SpecDialogSessionRepository(_context), Sandbox.Holds.None(), TimeProvider.System,
                NullLogger<SpecDialogSessionManager>.Instance),
            new SpecDialogScopeResolver(Loader()),
            new SpecDialogReplyComposer(),
            new SpecDialogMessenger([adapter.Object], NullLogger<SpecDialogMessenger>.Instance));

    private static Mock<IPlatformAdapter> Adapter(string platform, List<string>? sent = null)
    {
        var adapter = new Mock<IPlatformAdapter>();
        adapter.SetupGet(a => a.Platform).Returns(platform);
        adapter.Setup(a => a.SendInfoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string?, CancellationToken>(
                (_, _, text, _, _) => sent?.Add(text))
            .Returns(Task.CompletedTask);
        return adapter;
    }

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new() { Name = "sample", Repos = [new RepoConnection { Name = "repo-a" }] },
            },
        });
        return loader.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
