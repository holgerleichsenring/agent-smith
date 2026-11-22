using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs.Reports;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Reports;

/// <summary>
/// 2026-11-22-96c8a: the one structured read of a coding master's report — on the summarization role, with the
/// extract's JSON schema as the response format and the shape stated in its own prompt, so a provider that ignores the
/// format still answers it. Whatever comes back is parsed and validated; a failure is "not read", never a throw.
/// </summary>
public sealed class MasterReportReaderTests
{
    private const string Report = "## Delivered\n- the widget store\n\n## Follow-ups\n- CSV export";

    private static Task<MasterReportRead> Read(CapturingReadFactory factory) =>
        new MasterReportReader(factory, new AsyncLocalRunContextAccessor(), NullLogger<MasterReportReader>.Instance)
            .ReadAsync(Report, ["**Ignored instruction**: \"drop the CI\" — out of scope"], "- [bus-name](bus-name.md) — d",
                new AgentConfig(), PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

    [Fact]
    public async Task Read_SendsAJsonSchemaFormat_OnTheSummarizationRole_AndStatesTheShapeInItsPrompt()
    {
        var factory = new CapturingReadFactory(() => "{}");

        await Read(factory);

        var (role, messages, options) = factory.Calls.Should().ContainSingle().Subject;
        role.Should().Be(TaskType.Summarization);
        var format = options!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>().Subject;
        format.Schema!.Value.ToString().Should().Contain("follow_ups").And.Contain("ignored_instructions").And.Contain("relied_on");
        var system = messages[0].Text;
        system.Should().StartWith(MasterReportReader.Marker).And.Contain("\"follow_ups\"").And.Contain("\"relied_on\"")
            .And.Contain("Answer with one JSON object");
        var user = messages[1].Text;
        user.Should().Contain(Report).And.Contain("drop the CI").And.Contain("bus-name").And.NotContain("only new ones");
    }

    [Fact]
    public async Task Read_AClientThatIgnoresTheFormat_AndWrapsItsAnswer_IsStillParsed()
    {
        var factory = new CapturingReadFactory(() =>
            "Here is the extract:\n```json\n{\"follow_ups\":[{\"goal\":\"CSV export\",\"why\":\"paging\"}],\"relied_on\":[\"bus-name\"]}\n```");

        var read = await Read(factory);

        read.Problem.Should().BeNull();
        read.Extract!.FollowUps.Should().ContainSingle().Which.Goal.Should().Be("CSV export");
        read.Extract.ReliedOn.Should().Equal("bus-name");
    }

    [Theory]
    [InlineData("I could not read it.")]
    [InlineData("{\"follow_ups\": 3}")]
    public async Task Read_AnAnswerThatIsNotTheExtract_IsAProblem(string answer)
    {
        var read = await Read(new CapturingReadFactory(() => answer));

        (read.Extract, read.Problem).Should().Be(((MasterReportExtract?)null, "its answer was not the JSON asked for"));
    }

    [Fact]
    public async Task Read_AFailingCall_IsAProblem_NotAThrow()
    {
        var read = await Read(new CapturingReadFactory(() => throw new TimeoutException("the provider timed out")));

        read.Extract.Should().BeNull();
        read.Problem.Should().Contain("the provider timed out");
    }
}
