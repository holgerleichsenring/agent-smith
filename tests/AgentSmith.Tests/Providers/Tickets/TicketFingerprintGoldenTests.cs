using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// The fingerprint of a mapped ticket per tracker, pinned: every in-flight series compares a live ticket against
/// one it recorded, so a mapper change that moves it would read every ticket as edited and cut it again.
/// </summary>
public sealed class TicketFingerprintGoldenTests
{
    [Fact]
    public void Fingerprint_MappedAzureDevOpsTicket_IsUnchanged() =>
        TicketTextFingerprint.Of(new AzureDevOpsFieldMapper().Map(new TicketId("17"), TicketFieldFormatTests.AzureFields()))
            .Should().Be("1e80f780592ba678d607a2de1e5390f52c1a575e1a537375627c0647adca8311");

    [Fact]
    public void Fingerprint_MappedJiraTicket_IsUnchanged() =>
        TicketTextFingerprint.Of(new JiraFieldMapper().Map(new TicketId("AS-1"), TicketFieldFormatTests.JiraIssue()))
            .Should().Be("8437f2c1ea4eec1a8bc00eaa9ee3927527aa4bf3ec97d985d7e1dbe96b597170");

    [Fact]
    public void Fingerprint_MappedGitHubTicket_IsUnchanged() =>
        TicketTextFingerprint.Of(new GitHubFieldMapper().Map(new TicketId("7"), new SimpleJsonSerializer()
                .Deserialize<Issue>("""{ "number": 7, "title": "Export", "body": "## Goal\n- **CSV**", "state": "open" }""")))
            .Should().Be("b2efd6457b4d1728047c0c8b64f2ebde1804c7c7e2cec77cc502b0b6c19d07a7");

    [Fact]
    public void Fingerprint_MappedGitLabTicket_IsUnchanged() =>
        TicketTextFingerprint.Of(new GitLabFieldMapper().Map(new TicketId("7"), JsonDocument.Parse(
                """{ "iid": 7, "title": "Export", "description": "## Goal\n- **CSV**", "state": "opened" }""").RootElement))
            .Should().Be("b2efd6457b4d1728047c0c8b64f2ebde1804c7c7e2cec77cc502b0b6c19d07a7");
}
