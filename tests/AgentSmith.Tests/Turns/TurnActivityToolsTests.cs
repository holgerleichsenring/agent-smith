using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Turns;

/// <summary>
/// 2026-09-17-042ee: the tools a design turn is handed report what they were called with,
/// before they run — that is the whole point, since the call is what takes the time.
/// </summary>
public sealed class TurnActivityToolsTests
{
    [Fact]
    public async Task Activity_SpecDialogToolCall_ReportsToolAndSummary()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting([Read()]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["path"] = "sample-repo/src/Router.cs" }, CancellationToken.None);

        recorder.Lines.Should().Equal("tool read_file sample-repo/src/Router.cs");
    }

    [Fact]
    public async Task Activity_UrlArgument_IsReducedToItsHost()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string url) => "ok", "http_request")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["url"] = "https://docs.example.test/v2/orders?token=secret" },
            CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should().Be(
            "tool http_request docs.example.test", "a url's path and query are content of their own");
    }

    [Fact]
    public async Task Activity_RepositoryArgument_NamesTheRepositoryThePathIsIn()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string repository, string path) => "ok", "read_file")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["repository"] = "repo-a", ["path"] = "src/Api.cs" },
            CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should().Be(
            "tool read_file repo-a/src/Api.cs", "one repository's src/Api.cs is not another's");
    }

    [Fact]
    public async Task Activity_SearchPattern_IsShownBesideItsRepository_NotUnderIt()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string repository, string pattern) => "ok", "search_repository")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["repository"] = "repo-a", ["pattern"] = "Dispatch" },
            CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should().Be(
            "tool search_repository repo-a Dispatch", "'repo-a/Dispatch' would read as a file");
    }

    [Fact]
    public async Task Activity_SchemelessUrlArgument_LosesItsQueryString()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string url) => "ok", "http_request")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["url"] = "localhost:8080/v2/orders?token=secret" },
            CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should().Be(
            "tool http_request localhost:8080/v2/orders",
            "an address that does not parse as absolute http(s) still carries a query");
    }

    [Fact]
    public async Task Activity_UrlShapedSearchPattern_IsShownAsItWasSearched()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string pattern) => "ok", "grep_in_files")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["pattern"] = "https://api.example.test/v2\\?key=" },
            CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should().Be(
            "tool grep_in_files https://api.example.test/v2\\?key=",
            "a pattern reduced to a host hides what the turn actually looked for");
    }

    [Fact]
    public async Task Activity_NoObserverSet_ReportsNothing()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        accessor.Observe(recorder).Dispose(); // the turn that had set one has ended
        var ran = false;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string path) => { ran = true; return "ok"; }, "read_file")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["path"] = "sample-repo/src/Router.cs" }, CancellationToken.None);

        ran.Should().BeTrue("the tool runs whether or not anyone is listening");
        recorder.Seen.Should().BeEmpty("only a caller that set an observer hears anything");
    }

    [Fact]
    public async Task Activity_ReportedBeforeTheToolRuns()
    {
        var (accessor, recorder, observing) = Observed();
        using var _ = observing;
        var seenWhenInvoked = -1;
        var tools = TurnActivityRecorder.Tools(accessor).Reporting(
            [AIFunctionFactory.Create((string path) =>
            {
                seenWhenInvoked = recorder.Seen.Count;
                return "ok";
            }, "read_file")]);

        await tools[0].As<AIFunction>().InvokeAsync(
            new AIFunctionArguments { ["path"] = "sample-repo/src/Router.cs" }, CancellationToken.None);

        seenWhenInvoked.Should().Be(1, "a twenty-second read is announced when it starts, not when it ends");
    }

    [Fact]
    public void Activity_NonFunctionTool_PassesThroughUnwrapped()
    {
        var (accessor, _, observing) = Observed();
        using var handle = observing;
        var plain = new NotAFunction();

        var tools = TurnActivityRecorder.Tools(accessor).Reporting([plain, Read()]);

        tools[0].Should().BeSameAs(plain, "a surface returns AITools and only a function is invoked here");
        tools[1].Should().BeOfType<ActivityReportingAIFunction>();
    }

    private static AIFunction Read() =>
        AIFunctionFactory.Create((string path) => $"read {path}", "read_file");

    private static (ITurnActivityObserverAccessor Accessor, TurnActivityRecorder Recorder, IDisposable Observing)
        Observed()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        return (accessor, recorder, accessor.Observe(recorder));
    }

    private sealed class NotAFunction : AITool;
}
