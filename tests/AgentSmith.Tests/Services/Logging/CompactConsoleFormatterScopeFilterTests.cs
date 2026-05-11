using AgentSmith.Server.Services.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Logging;

/// <summary>
/// CompactConsoleFormatter is supposed to render operator-defined scopes
/// (`run=...`, `ticket=...` from `BeginScope`) and drop the noisy framework
/// scopes that ASP.NET Core / Kestrel / Activity tracking stamp on every
/// request-path log line (SpanId, ConnectionId, RequestPath, …).
/// </summary>
public sealed class CompactConsoleFormatterScopeFilterTests
{
    [Fact]
    public void Write_OperatorScopes_AreRenderedInOrder()
    {
        var output = FormatLineWithScopes("hello", ["run=abcd1234", "ticket=18750"]);

        output.Should().Contain("[run=abcd1234]");
        output.Should().Contain("[ticket=18750]");
        output.IndexOf("[run=abcd1234]").Should().BeLessThan(output.IndexOf("[ticket=18750]"));
    }

    [Theory]
    [InlineData("SpanId:abc, TraceId:def, ParentId:0")]
    [InlineData("ConnectionId:0HNLFJ58JEFO5")]
    [InlineData("RequestPath:/health RequestId:0HNLFJ58JEFO5:00000001")]
    [InlineData("HTTP GET https://api.github.com/repos/x/y/issues/42")]
    [InlineData("HTTP PATCH https://dev.azure.com/...")]
    public void Write_FrameworkScopes_AreDropped(string frameworkScope)
    {
        var output = FormatLineWithScopes("hello", [frameworkScope]);

        output.Should().NotContain("[" + frameworkScope + "]");
    }

    [Fact]
    public void Write_MixedScopes_OnlyOperatorScopesSurvive()
    {
        var output = FormatLineWithScopes("hello", [
            "SpanId:abc, TraceId:def",
            "run=abcd1234",
            "RequestPath:/webhook",
            "ticket=18750"
        ]);

        output.Should().Contain("[run=abcd1234]");
        output.Should().Contain("[ticket=18750]");
        output.Should().NotContain("SpanId:");
        output.Should().NotContain("RequestPath:");
    }

    private static string FormatLineWithScopes(string message, string[] scopeStrings)
    {
        var formatter = new CompactConsoleFormatter();
        var scopeProvider = new StubScopeProvider(scopeStrings);
        using var writer = new StringWriter();

        var entry = new LogEntry<string>(
            LogLevel.Information,
            "TestCategory.Tests",
            eventId: 0,
            state: message,
            exception: null,
            formatter: (s, _) => s);

        formatter.Write(in entry, scopeProvider, writer);
        return writer.ToString();
    }

    private sealed class StubScopeProvider(string[] scopes) : IExternalScopeProvider
    {
        public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
        {
            foreach (var s in scopes) callback(s, state);
        }

        public IDisposable Push(object? state) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
