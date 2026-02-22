using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class CommandContextFactoryTests
{
    private readonly CommandContextFactory _sut = new();

    [Fact]
    public void Create_FetchTicketCommand_ReturnsFetchTicketContext()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("123"));

        var result = _sut.Create("FetchTicketCommand", project, pipeline);

        result.Should().BeOfType<FetchTicketContext>();
        var ctx = (FetchTicketContext)result;
        ctx.TicketId.Value.Should().Be("123");
    }

    [Fact]
    public void Create_CheckoutSourceCommand_CreatesBranchFromTicket()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("456"));

        var result = _sut.Create("CheckoutSourceCommand", project, pipeline);

        result.Should().BeOfType<CheckoutSourceContext>();
        var ctx = (CheckoutSourceContext)result;
        ctx.Branch.Value.Should().Be("fix/456");
    }

    [Fact]
    public void Create_GeneratePlanCommand_PullsFromPipeline()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("1"), "Title", "Desc", null, "Open", "GitHub"));
        pipeline.Set(ContextKeys.CodeAnalysis, new CodeAnalysis(
            Array.Empty<string>(), Array.Empty<string>(), null, null));
        pipeline.Set(ContextKeys.CodingPrinciples, "principles");

        var result = _sut.Create("GeneratePlanCommand", project, pipeline);

        result.Should().BeOfType<GeneratePlanContext>();
        var ctx = (GeneratePlanContext)result;
        ctx.Ticket.Title.Should().Be("Title");
        ctx.CodingPrinciples.Should().Be("principles");
    }

    [Fact]
    public void Create_UnknownCommand_ThrowsConfigurationException()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();

        var act = () => _sut.Create("UnknownCommand", project, pipeline);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown command*");
    }

    private static ProjectConfig CreateProjectConfig()
    {
        return new ProjectConfig
        {
            Source = new SourceConfig { Type = "local", Path = "/tmp" },
            Tickets = new TicketConfig { Type = "github", Url = "https://github.com/test/repo" },
            Agent = new AgentConfig { Type = "claude", Model = "sonnet" },
            Pipeline = "fix-bug",
            CodingPrinciplesPath = "config/coding-principles.md"
        };
    }
}
