using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Output;

public sealed class ChannelSplitTests
{
    [Fact]
    public async Task Console_DetailsPresent_StillRendersOnlyDescriptionTitle()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Short headline", "fix it", 80) with
            {
                Details = "This long details body should NOT appear in console output."
            }
        };
        var pipeline = new PipelineContext();
        var ctx = new OutputContext("test", null, observations, null, "./test-output", pipeline);
        var sut = new ConsoleOutputStrategy(NullLogger<ConsoleOutputStrategy>.Instance);

        var output = await CaptureConsoleAsync(() => sut.DeliverAsync(ctx));

        output.Should().Contain("Short headline");
        output.Should().NotContain("This long details body");
    }

    [Fact]
    public async Task Summary_DetailsPresent_StillRendersOnlyDescription()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Short headline", "fix it", 80) with
            {
                Details = "Summary should NOT include this long body either."
            }
        };
        var pipeline = new PipelineContext();
        var ctx = new OutputContext("test", null, observations, null, "./test-output", pipeline);
        var sut = new SummaryOutputStrategy(NullLogger<SummaryOutputStrategy>.Instance);

        var output = await CaptureConsoleAsync(() => sut.DeliverAsync(ctx));

        output.Should().Contain("Short headline");
        output.Should().NotContain("Summary should NOT include");
    }

    [Fact]
    public void Sarif_DetailsPresent_PutsDetailsInProperties()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Headline", "fix it", 80) with
            {
                Details = "SARIF detailed_message body content."
            }
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(observations).ToJsonString();

        sarif.Should().Contain("detailed_message");
        sarif.Should().Contain("SARIF detailed_message body content");
    }

    [Fact]
    public void Sarif_DetailsNull_NoDetailedMessageProperty()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "Headline only", "fix it", 80)
        };

        var sarif = SarifOutputStrategy.BuildSarifDocument(observations).ToJsonString();

        sarif.Should().NotContain("detailed_message");
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task> action)
    {
        var original = Console.Out;
        await using var capture = new StringWriter();
        Console.SetOut(capture);
        try { await action(); }
        finally { Console.SetOut(original); }
        return capture.ToString();
    }
}
