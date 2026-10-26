using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Registry;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Surface;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Json;

/// <summary>
/// 2026-10-26-cb25d: the answer matrix every JSON-from-a-model-answer reader is pinned on — nine framings of one
/// payload per reader, each reduced to an outcome (a row count, a message, a verdict, null or throw).
/// </summary>
internal static class JsonAnswerReaderMatrix
{
    public static readonly string[] Frames =
        ["bare", "fenced", "prose", "cited-before", "cited-after", "two-values", "truncated", "empty", "none"];

    public static string Outcomes(string reader)
    {
        var (payload, cite, read) = Readers[reader];
        string[] texts =
        [
            payload, $"```json\n{payload}\n```", $"Here it is:\n{payload}\nThat is all.", $"Checked {cite} first.\n{payload}",
            $"{payload}\nSee {cite}.", $"{payload}\n{payload}", payload[..^3], "   ", "no json here",
        ];
        return string.Join(",", texts.Select(t => Outcome(read, t)));
    }

    private static string Outcome(Func<string, string?> read, string text)
    {
        try { return read(text) ?? "null"; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return "throw"; }
    }

    private const string Rows = """[{"criterion":"c1","disposition":"satisfied"},{"criterion":"c2","disposition":"satisfied"}]""";
    private const string Refutations = """[{"location":"a","substantiated":true,"id":"f1"},{"location":"b","substantiated":false,"id":"f2"}]""";
    private const string Cuts = """[{"spec_id":"s1","criterion":"c","problem":"p","why":"w"},{"spec_id":"s2","criterion":"c","problem":"p","why":"w"}]""";
    private const string Verdict = """{"verdict":"BUILD","cuts":[],"findings":[]}""";
    private const string Usage = """{"call_sites":[{"file":"a.ts","operation":"getX","sends":[],"reads":[]}],"undecided":[]}""";
    private const string Staged = """{"files":[{"path":".npmrc","content":"x"}]}""";
    private const string ProviderError = """{"type":"error","error":{"message":"overloaded"}}""";
    private const string AppIntent = """{"pipeline":"code","project":"p","ticket_id":"12","confidence":0.9}""";
    private const string ServerIntent = """{"commandType":"fix","ticketNumber":"12","project":"p","title":"","confidence":"high"}""";

    private static readonly Dictionary<string, (string Payload, string Cite, Func<string, string?> Read)> Readers = new()
    {
        ["SpecAccountReader"] = (Rows, "[c2]", t => SpecAccountReader.Read(t)?.Count.ToString()),
        ["FindingRefutationReader"] = (Refutations, "[c2]", t => Refuter().Read(t)?.Count.ToString()),
        ["SpecCutAnswerReader"] = (Cuts, "[c2]", t => SpecCutAnswerReader.Read(t)?.Count.ToString()),
        ["SpecAttackAnswerReader"] = (Verdict, "{x}", t => SpecAttackAnswerReader.Read(t)?.Verdict),
        ["SeriesContractAnswerReader"] = (Verdict, "{x}", t => SeriesContractAnswerReader.Read(t)?.Verdict),
        ["ClientUsageReportReader"] = (Usage, "{x}", t => ClientUsageReportReader.Read(t)?.CallSites.Count.ToString()),
        ["StagedAuthFileJsonReader"] = (Staged, "{x}", t => new StagedAuthFileJsonReader().Read(t).Count.ToString()),
        ["ProviderErrorPayloadParser"] = (ProviderError, "{x}", ProviderErrorPayloadParser.TryExtractMessage),
        ["Application.LlmIntentParser"] = (AppIntent, "{x}", AppIntentOf),
        ["Server.LlmIntentParser"] = (ServerIntent, "{x}", ServerIntentOf),
    };

    private static FindingRefutationReader Refuter() => new(
        new TolerantJsonParser(Mock.Of<ITolerantParseTelemetry>(), NullLogger<TolerantJsonParser>.Instance),
        NullLogger<FindingRefutationReader>.Instance);

    private static string? AppIntentOf(string text)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());
        var parser = new AgentSmith.Application.Services.LlmIntentParser(Answering(text), loader.Object, new AgentConfig(),
            Mock.Of<IRunContextAccessor>(), NullLogger<AgentSmith.Application.Services.LlmIntentParser>.Instance);
        return parser.ParseToPipelineRequestAsync("input", "config.yml", CancellationToken.None).GetAwaiter().GetResult()
            .TicketId?.Value;
    }

    private static string? ServerIntentOf(string text)
    {
        var parser = new AgentSmith.Server.Services.LlmIntentParser(Answering(text), new AgentConfig(),
            NullLogger<AgentSmith.Server.Services.LlmIntentParser>.Instance);
        var intent = parser.ParseAsync("input", "u", "c", "slack", CancellationToken.None).GetAwaiter().GetResult();
        return (intent as AgentSmith.Server.Models.FixTicketIntent)?.TicketId;
    }

    private static IChatClientFactory Answering(string text)
    {
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        var factory = new Mock<IChatClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<AgentConfig>(), It.IsAny<TaskType>())).Returns(chat.Object);
        return factory.Object;
    }
}
