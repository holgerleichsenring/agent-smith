using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class ApprovalHandlerTests
{
    private readonly ApprovalHandler _handler = new(
        NullLoggerFactory.Instance.CreateLogger<ApprovalHandler>());

    [Fact]
    public async Task ExecuteAsync_HeadlessMode_AutoApproves()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Headless, true);
        var plan = new Plan("Test", new List<PlanStep>(), "{}");
        pipeline.Set(ContextKeys.Plan, plan);
        var context = new ApprovalContext(plan, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("approved");
        pipeline.Get<bool>(ContextKeys.Approved).Should().BeTrue();
    }
}
