using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// p0503c: the sandbox agent's copy of the formatter moves in lockstep with the server's,
/// as its own header demands. The agent has no web host and can never format a request
/// path, so this asserts the lockstep — not that the agent leaks anything today. The two
/// copies cannot share a test: the agent is a standalone Exe with no reference to the
/// server, and that is the same reason the duplication exists at all.
/// </summary>
public sealed class CompactConsoleFormatterRedactionTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.payload.signature";

    [Fact]
    public void AgentFormatter_MessageCarriesAnAccessToken_TheValueIsNotWritten()
    {
        var formatter = new CompactConsoleFormatter();
        using var writer = new StringWriter();
        var entry = new LogEntry<string>(
            LogLevel.Information, "TestCategory.Tests", eventId: 0,
            state: $"connecting with access_token={Token}", exception: null,
            formatter: (s, _) => s);

        formatter.Write(in entry, scopeProvider: null, writer);

        writer.ToString().Should().NotContain(Token);
        writer.ToString().Should().Contain("access_token=***");
    }
}
