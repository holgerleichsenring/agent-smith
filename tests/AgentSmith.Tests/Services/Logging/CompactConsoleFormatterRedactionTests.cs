using AgentSmith.Server.Services.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Logging;

/// <summary>
/// p0503c: the hub handshake token rides in the query string, so any URL-shaped string
/// that reaches this formatter carries a live credential. The scrub is applied to the
/// composed line, which is why the exception chain is covered by the same rule as the
/// message.
/// </summary>
public sealed class CompactConsoleFormatterRedactionTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.payload.signature";

    [Fact]
    public void Formatter_MessageCarriesAnAccessToken_TheValueIsNotWritten()
    {
        var output = Format($"Request starting HTTP/1.1 GET /hub/jobs?id=x&access_token={Token}");

        output.Should().NotContain(Token);
        output.Should().Contain("access_token=***");
        output.Should().Contain("/hub/jobs?id=x", "everything but the credential still reads");
    }

    [Fact]
    public void Formatter_ExceptionMessageCarriesAnAccessToken_TheValueIsNotWritten()
    {
        var inner = new InvalidOperationException($"IDX10000: access_token={Token} was rejected");
        var output = Format("handshake failed", new InvalidOperationException("outer", inner));

        output.Should().NotContain(Token);
        output.Should().Contain("access_token=***");
    }

    [Fact]
    public void Formatter_QueryWithoutAToken_IsWrittenUnchanged()
    {
        const string message = "Request starting HTTP/1.1 GET /hub/jobs?id=x&negotiateVersion=1";

        Format(message).Should().Contain(message);
    }

    private static string Format(string message, Exception? exception = null)
    {
        var formatter = new CompactConsoleFormatter();
        using var writer = new StringWriter();
        var entry = new LogEntry<string>(
            LogLevel.Information, "TestCategory.Tests", eventId: 0,
            state: message, exception: exception, formatter: (s, _) => s);

        formatter.Write(in entry, scopeProvider: null, writer);
        return writer.ToString();
    }
}
