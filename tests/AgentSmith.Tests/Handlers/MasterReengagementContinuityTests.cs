using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0341f: when the open loop re-engages, the next pass must receive what the previous
/// pass established.
/// <para>
/// p0341d promised this and its criterion — "re-engagement now fires only on genuine
/// breaks … proven by an integration test that completes a long multi-step run in one
/// continuous pass" — was signed off by a test that builds the conversation by hand and
/// never involves the component that drops it. The compaction middleware preserves the
/// thread WITHIN a pass; the pass BOUNDARY throws it away, because the handler composes a
/// fresh <see cref="AgenticLoopRequest"/> per pass and the runner opens every request with
/// exactly two messages.
/// </para>
/// <para>
/// What that costs is not theoretical: on live run 98b9 the master re-engaged 34 times,
/// each pass re-reading the same file and re-running the same four greps, because the
/// nudge tells it to "resume from where you left off" and hands it no left-off. This suite
/// states the property at the seam where it is decided — what the NEXT pass is given —
/// rather than at the class that was already green while the run was not.
/// </para>
/// </summary>
public sealed class MasterReengagementContinuityTests
{
    private const string Established =
        "MessageBroker.cs is the only place that registers the legacy consumer";

    /// <summary>
    /// A verdict that is honest and still leaves the contract open: the pass reports what
    /// it did, one ratified criterion is not yet met, so the loop re-engages. This is the
    /// ordinary mid-work shape, not a failure path.
    /// </summary>
    private const string OpenVerdict = """
        ```verdict
        { "status": "green", "build_ran": true, "build_passed": true, "tests_ran": true,
          "tests_passed": true, "summary": "inventory written",
          "acceptance": [ { "criterion": "the consumer registration is migrated",
                            "status": "unmet", "evidence": "not started" } ] }
        ```
        """;

    [Fact]
    public async Task ASecondPass_IsGivenWhatTheFirstPassEstablished()
    {
        var loop = new ScriptedPasses($"{Established}\n{OpenVerdict}", $"still working\n{OpenVerdict}");

        await RunTwoPassesAsync(loop);

        loop.Seen.Should().HaveCountGreaterThan(1,
            "the contract is unsatisfied and the pass called a tool, so the loop re-engages");
        Conveyed(loop.Seen[1]).Should().Contain(Established,
            "a pass told to resume from where it left off must be given where that was — "
            + "otherwise re-deriving the same facts is the only way it can comply");
    }

    /// <summary>
    /// The tool results are the expensive half: the master pays for a grep once and then
    /// pays again every pass that cannot see it.
    /// </summary>
    [Fact]
    public async Task ASecondPass_IsGivenTheToolResultsTheFirstPassPaidFor()
    {
        const string toolResult = "src/Messaging/MessageBroker.cs:41: AddLegacyConsumer<T>()";
        var loop = new ScriptedPasses($"searched\n{OpenVerdict}", $"still working\n{OpenVerdict}")
        {
            ToolResult = toolResult,
        };

        await RunTwoPassesAsync(loop);

        Conveyed(loop.Seen[1]).Should().Contain(toolResult);
    }

    private static async Task RunTwoPassesAsync(ScriptedPasses loop)
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body");
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.PipelineName, "add-feature");
        const string yaml = """
            phase: p1
            goal: migrate the consumer registration
            done:
              - the consumer registration is migrated
            """;
        context.Pipeline.Set(ContextKeys.PhaseSpec,
            new PhaseDraft("p1", "migrate the consumer registration", yaml, [])
            {
                Done = ["the consumer registration is migrated"],
            });

        await MasterHandlerFixture.Build(loop, prompts).ExecuteAsync(context, CancellationToken.None);
    }

    /// <summary>
    /// Everything the pass puts in front of the model, whichever field carries it — the
    /// claim is about what the model can see, not about where we chose to put it.
    /// </summary>
    private static string Conveyed(AgenticLoopRequest request) =>
        string.Join("\n",
            [request.SystemPrompt, request.UserPrompt,
             .. (request.PriorMessages ?? []).SelectMany(m => m.Contents).Select(Rendered)]);

    // A tool RESULT is not TextContent, and it is the half the master paid a sandbox
    // round-trip for — so the helper reads content, not just prose.
    private static string Rendered(AIContent content) => content switch
    {
        TextContent text => text.Text,
        FunctionResultContent result => result.Result?.ToString() ?? string.Empty,
        _ => string.Empty,
    };

    /// <summary>
    /// A loop runner that answers with scripted text and reports a tool call per pass, so
    /// the driver sees a pass that did something (the one stall signal it reads) and
    /// re-engages.
    /// </summary>
    private sealed class ScriptedPasses(params string[] texts) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        private int _call;

        internal string? ToolResult { get; init; }

        internal IReadOnlyList<AgenticLoopRequest> Seen => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var text = texts[Math.Min(_call, texts.Length - 1)];
            _call++;

            var call = new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"c{_call}", "grep_in_tree", new Dictionary<string, object?>())]);
            var result = new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent($"c{_call}", ToolResult ?? "no matches")]);
            var answer = new ChatMessage(ChatRole.Assistant, text);

            var response = new ChatResponse([call, result, answer])
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
