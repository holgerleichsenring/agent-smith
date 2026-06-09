using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class TicketWriteAuditTests
{
    [Fact]
    public void Caller_NamesTheAgentSmithCallerWithFileAndLine()
    {
        // p0260: the audit must attribute a ticket write to the exact agent-smith
        // frame that issued it. Called from this test method (invoked via
        // reflection, so never inlined away), the chain must name this method +
        // file with framework frames stripped — proving it would name a phantom
        // in-progress writer the same way. `because` echoes the chain on failure.
        var chain = TicketWriteAudit.Caller();

        chain.Should().Contain("TicketWriteAuditTests", $"chain was: {chain}");
        chain.Should().Contain(nameof(Caller_NamesTheAgentSmithCallerWithFileAndLine), $"chain was: {chain}");
        chain.Should().Contain(".cs:", $"chain was: {chain}");
        chain.Should().NotContain("System.", $"chain was: {chain}");
        // the helper strips its OWN frame ("TicketWriteAudit.Caller"); the test
        // class shares the prefix, so target the method form precisely.
        chain.Should().NotContain($"{nameof(TicketWriteAudit)}.{nameof(TicketWriteAudit.Caller)}", $"chain was: {chain}");
    }
}
