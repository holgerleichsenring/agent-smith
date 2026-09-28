using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Services.Dialogue;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// One server process's view of chat-started runs, composed through the production
/// registration over a real migrated SQLite store. Only the edges are doubles: the chat
/// platforms, the live dialogue stream and the hot answer stream. Two harnesses over one
/// connection are two processes — a restart, or a second replica — sharing one database.
/// </summary>
internal sealed class ChatRunHarness : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    public ChatRunHarness(SqliteConnection? shared = null)
    {
        _ownsConnection = shared is null;
        _connection = shared ?? MigratedStoreTemplate.OpenCopy();
        _services = Compose().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public SqliteConnection Connection => _connection;
    public RecordingThreadAdapter Slack { get; } = new("slack");
    public RecordingThreadAdapter Teams { get; } = new("teams", "https://smba.example/emea");
    public StubQuestionReader LiveQuestions { get; } = new();
    public HotStreamRecorder HotStream { get; } = new();
    public SettableClock Clock { get; } = new();
    public AgentSmithConfig Config { get; } = new();

    public T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    public void SeedRun(string runId, string status, bool finished, string? summary = null, params string[] prUrls)
    {
        using var ctx = MigratedStoreTemplate.Context(_connection);
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "sample", Pipeline = "code", Status = status, Summary = summary,
            StartedAt = Clock.Now, FinishedAt = finished ? Clock.Now : null,
        });
        foreach (var (url, i) in prUrls.Select((u, i) => (u, i)))
            ctx.RunRepos.Add(new RunRepo { RunId = runId, RepoName = $"repo-{i}", PrUrl = url });
        ctx.SaveChanges();
    }

    public Task ParkAsync(string runId, DialogQuestion question) =>
        Get<IRunCheckpointStore>().SaveAsync(new RunCheckpointRecord(
            runId, "sample", "42", "github", "code", runId, question.QuestionId,
            System.Text.Json.JsonSerializer.Serialize(question), "[]", "{}", 1,
            Clock.Now, Clock.Now.AddDays(3), null), CancellationToken.None);

    public void Dispose()
    {
        _services.Dispose();
        if (_ownsConnection) _connection.Dispose();
    }

    private ServiceCollection Compose()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IUnitOfWork>(_ => MigratedStoreTemplate.Context(_connection));
        services.AddSingleton<IUniqueViolationTranslator, SqliteUniqueViolationTranslator>();
        services.AddScoped<RunCheckpointRepository>().AddScoped<DialogueAnswerRepository>();
        services.AddSingleton<IRunCheckpointStore, DbRunCheckpointStore>();
        services.AddSingleton<IDialogueAnswerInbox, DbDialogueAnswerInbox>();
        services.AddSingleton<IDialogueTransport>(sp =>
            new DurableDialogueTransport(HotStream, sp.GetRequiredService<IDialogueAnswerInbox>()));
        services.AddSingleton<IChatThreadAdapter>(Slack).AddSingleton<IChatThreadAdapter>(Teams);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Config);
        services.AddSingleton(loader.Object);
        services.AddSingleton(new ServerContext("agentsmith.yml"));
        // The startup configuration names no dashboard: a link can only come from the live one.
        services.AddSingleton(new RunAnswerLink(new AgentSmithConfig()));
        services.AddChatRunBinding();
        services.AddSingleton<IDialogueQuestionReader>(LiveQuestions);
        services.AddTransient<ChatLaunchAnnouncer>().AddTransient<ChatRunStart>();
        return services;
    }
}
