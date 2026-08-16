using AgentSmith.Application.Services.Trace;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Trace;

/// <summary>
/// p0427: a recorded run is only an instrument if it can be REPLAYED — read back in order,
/// served to the framework without a provider, and exported to something a test suite can
/// keep. Measured 2026-08-14..16: 45 runs and 33h52m to find defects that were all
/// deterministic and local.
/// </summary>
public sealed class ReplayFromRecordTests
{
    [Fact]
    public void ARecordedRun_ReadsBackInTheOrderItWasWritten()
    {
        var trace = RecordedTrace.Of([
            Entry(10, RecordedTrace.AnswerLabel, "second"),
            Entry(3, RecordedTrace.AnswerLabel, "first"),
            Entry(4, "tool", "a tool result the model received"),
        ]);

        // Sequence 10 comes after 3 as a NUMBER, not as the text "10"; tool and prompt
        // entries are evidence for a human, not answers a replay serves.
        trace.Answers.Should().Equal(["first", "second"]);
    }

    [Fact]
    public void TheKeyThatWritesAnEntry_IsTheKeyThatReadsItBack()
    {
        var key = RecordedTraceKey.Format(7, RecordedTrace.AnswerLabel);

        key.Should().Be("trace/0007.answer");
        RecordedTraceKey.TryParse(key, "content", out var entry).Should().BeTrue();
        entry.Should().Be(new RecordedTraceEntry(7, "answer", "content"));
    }

    [Fact]
    public void ARecordedAnswer_CarriesItsToolCalls_AndReplaysAsThem()
    {
        var recorded = TracedAnswer.Render(new ChatResponse(new ChatMessage(
            ChatRole.Assistant,
            [
                new TextContent("Patching the guard."),
                new FunctionCallContent("call_1", "write_file", new Dictionary<string, object?>
                {
                    ["path"] = "primary/src/Patch.cs",
                    ["content"] = "// fix",
                }),
            ])));

        var replayed = TracedAnswer.Parse(recorded);

        replayed.Text.Should().Contain("Patching the guard.");
        var call = replayed.Messages.SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>().Single();
        call.Name.Should().Be("write_file");
        call.CallId.Should().Be("call_1");
        call.Arguments!["path"]!.ToString().Should().Be("primary/src/Patch.cs");
    }

    [Fact]
    public void APlainTextAnswer_ReplaysUnchanged()
    {
        const string answer = """Done. {"status":"green","summary":"patched"}""";

        TracedAnswer.Parse(TracedAnswer.Render(answer, [])).Text.Should().Be(answer);
    }

    [Fact]
    public async Task TheReplay_ServesTheRecordedAnswers_InOrder()
    {
        var client = new ReplayChatClient(RecordedTrace.Of([
            Entry(1, RecordedTrace.PromptLabel, "what the model saw"),
            Entry(2, RecordedTrace.AnswerLabel, "first answer"),
            Entry(3, RecordedTrace.AnswerLabel, "second answer"),
        ]));

        (await Ask(client)).Text.Should().Be("first answer");
        (await Ask(client)).Text.Should().Be("second answer");
        client.Served.Should().Be(2);
        client.Remaining.Should().Be(0, "prompts are evidence for a human, not answers to serve");
    }

    /// <summary>
    /// A recording stops where the run stopped, and the recordings worth replaying are of
    /// runs that DIED. Inventing an answer past the end would make the scenario pass for
    /// reasons the recording never contained.
    /// </summary>
    [Fact]
    public async Task TheReplay_PastTheEndOfAnIncompleteRecord_RaisesExhaustion()
    {
        var client = new ReplayChatClient(
            RecordedTrace.Of([Entry(1, RecordedTrace.AnswerLabel, "the last thing it said")]));
        await Ask(client);

        var act = async () => await Ask(client);

        (await act.Should().ThrowAsync<RecordedTraceExhaustedException>()).Which
            .Served.Should().Be(1);
    }

    [Fact]
    public async Task ARecordedRun_ExportsToFiles_AndLoadsBack()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"agentsmith-replay-{Guid.NewGuid():N}");
        var trace = RecordedTrace.Of([
            Entry(1, RecordedTrace.PromptLabel, "=== system ===\nbe useful"),
            Entry(2, RecordedTrace.AnswerLabel, "an answer"),
        ]);
        try
        {
            await RecordedTraceFiles.SaveAsync(trace, directory, CancellationToken.None);
            var loaded = await RecordedTraceFiles.LoadAsync(directory, CancellationToken.None);

            loaded.Entries.Should().BeEquivalentTo(trace.Entries);
            File.Exists(Path.Combine(directory, "0002.answer")).Should().BeTrue(
                "the file name is the store key without its prefix, so an export is readable");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Task<ChatResponse> Ask(ReplayChatClient client) =>
        client.GetResponseAsync([new ChatMessage(ChatRole.User, "anything")]);

    private static RecordedTraceEntry Entry(int sequence, string label, string content) =>
        new(sequence, label, content);
}
