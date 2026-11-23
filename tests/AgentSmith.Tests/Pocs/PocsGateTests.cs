using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933da: the pocs switch — off answers an empty 404 before the handler; on lets the request through.</summary>
public sealed class PocsGateTests
{
    [Fact]
    public void Enabled_ReadsThePocsSwitch()
    {
        var gate = Gate(enabled: false);

        gate.Enabled(new AgentSmithConfig { Pocs = new PocsConfig { Enabled = true } }).Should().BeTrue();
        gate.Enabled(new AgentSmithConfig()).Should().BeFalse("absent reads off");
    }

    [Fact]
    public async Task InvokeAsync_Off_IsNotFoundWithoutRunningTheHandler_On_RunsIt()
    {
        var ran = 0;
        EndpointFilterDelegate next = _ => { ran++; return ValueTask.FromResult<object?>("handled"); };
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());

        (await Gate(enabled: false).InvokeAsync(context, next)).Should().BeOfType<NotFound>();
        ran.Should().Be(0);
        (await Gate(enabled: true).InvokeAsync(context, next)).Should().Be("handled");
    }

    private static PocsGate Gate(bool enabled) =>
        new(new AgentSmith.Server.Services.Tickets.TicketProjects(
            new ProjectScopeUnderTest.FixedLoader(new AgentSmithConfig { Pocs = new PocsConfig { Enabled = enabled } }),
            new ServerContext("agentsmith.yml")));
}
