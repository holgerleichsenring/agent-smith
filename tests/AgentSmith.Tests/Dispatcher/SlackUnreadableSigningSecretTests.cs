using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// 2026-11-18-78efb: a Slack signing secret whose stored value exists but cannot be opened refuses every request and
/// the log names the secret and why; no signing secret configured still skips verification.
/// </summary>
public sealed class SlackUnreadableSigningSecretTests
{
    [Fact]
    public async Task Verify_AnUnreadableSigningSecret_Refuses_AndLogsTheSecretAndWhy()
    {
        var logs = new RecordingLoggerProvider();
        var options = new SlackAdapterOptions(name => name == SlackAdapterOptions.SigningSecretVariable
            ? CredentialLookup.Unreadable(name, "no master key is configured (key env:1)") : CredentialLookup.None);

        var verified = await Verifier(options.SigningSecretLookup, logs).VerifyAsync(new DefaultHttpContext().Request, CancellationToken.None);

        verified.Should().BeFalse();
        logs.Entries.Should().Contain(e => e.Level == LogLevel.Error
            && e.Message.Contains(SlackAdapterOptions.SigningSecretVariable) && e.Message.Contains("no master key"));
    }

    [Fact]
    public async Task Verify_NoSigningSecretConfigured_StillSkips()
    {
        var verified = await Verifier(new SlackAdapterOptions().SigningSecretLookup, new RecordingLoggerProvider())
            .VerifyAsync(new DefaultHttpContext().Request, CancellationToken.None);

        verified.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_AnAssignedSigningSecret_WinsOverTheReader_AndAnUnsignedRequestIsRefused()
    {
        var options = new SlackAdapterOptions(_ => CredentialLookup.Unreadable("x", "y")) { SigningSecret = "assigned-secret" };

        options.SigningSecretLookup.State.Should().Be(CredentialLookupState.Value);
        (await Verifier(options.SigningSecretLookup, new RecordingLoggerProvider())
            .VerifyAsync(new DefaultHttpContext().Request, CancellationToken.None)).Should().BeFalse();
    }

    private static SlackSignatureVerifier Verifier(CredentialLookup secret, RecordingLoggerProvider logs) =>
        new(secret, new Logger<SlackSignatureVerifier>(new LoggerFactory([logs])));
}
