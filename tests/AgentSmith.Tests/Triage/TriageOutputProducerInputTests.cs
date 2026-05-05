using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// Covers the no-ticket fallback in TriageOutputProducer.ResolveTicketOrSyntheticInput.
/// Regression guard against the p0111c regression where api-security-scan and
/// security-scan (both ticket-less pipelines) crashed Triage with
/// KeyNotFoundException 'Ticket'.
/// </summary>
public sealed class TriageOutputProducerInputTests
{
    [Fact]
    public void ResolveTicketOrSyntheticInput_TicketPresent_ReturnsTicketTitleAndDescription()
    {
        var pipeline = new PipelineContext();
        var ticket = new Ticket(
            new TicketId("T-1"),
            "Fix login bug",
            "Users cannot log in after password reset",
            acceptanceCriteria: null,
            status: "Open",
            source: "Test",
            labels: new[] { "bug", "auth" });
        pipeline.Set(ContextKeys.Ticket, ticket);

        var (text, labels) = TriageOutputProducer.ResolveTicketOrSyntheticInput(pipeline);

        text.Should().Contain("Fix login bug");
        text.Should().Contain("Users cannot log in after password reset");
        labels.Should().BeEquivalentTo(new[] { "bug", "auth" });
    }

    [Fact]
    public void ResolveTicketOrSyntheticInput_NoTicket_ReturnsSecurityScanSyntheticInput()
    {
        var pipeline = new PipelineContext();

        var (text, labels) = TriageOutputProducer.ResolveTicketOrSyntheticInput(pipeline);

        text.Should().StartWith("Security scan");
        text.Should().Contain("No ticket context");
        labels.Should().BeEmpty();
    }

    [Fact]
    public void ResolveTicketOrSyntheticInput_NoTicketButSwaggerSpecPresent_ReturnsApiScanSyntheticInput()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SwaggerSpec, new object());

        var (text, labels) = TriageOutputProducer.ResolveTicketOrSyntheticInput(pipeline);

        text.Should().StartWith("API security scan");
        text.Should().Contain("No ticket context");
        labels.Should().BeEmpty();
    }
}
