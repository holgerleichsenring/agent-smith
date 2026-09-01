using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class CommandContextFactoryTests
{
    private static readonly KeyedContextBuilder[] Builders =
    [
        new(CommandNames.FetchTicket, new FetchTicketContextBuilder()),
        new(CommandNames.CheckoutSource, new CheckoutSourceContextBuilder()),
        new(CommandNames.LoadCodingPrinciples, new LoadCodingPrinciplesContextBuilder()),
        new(CommandNames.LoadContext, new LoadContextContextBuilder()),
        new(CommandNames.AnalyzeCode, new AnalyzeCodeContextBuilder()),
        new(CommandNames.Approval, new ApprovalContextBuilder()),
        new(CommandNames.WriteRunResult, new WriteRunResultContextBuilder()),
        new(CommandNames.CommitAndPR, new CommitAndPRContextBuilder()),
        new(CommandNames.InitCommit, new InitCommitContextBuilder()),
        new(CommandNames.SwitchSkill, new SwitchSkillContextBuilder()),
        new(CommandNames.GenerateTests, new GenerateTestsContextBuilder()),
        new(CommandNames.GenerateDocs, new GenerateDocsContextBuilder()),
        new(CommandNames.LoadSwagger, new LoadSwaggerContextBuilder()),
        new(CommandNames.SpawnNuclei, new SpawnNucleiContextBuilder()),
        new(CommandNames.CompileFindings, new CompileFindingsContextBuilder()),
        new(CommandNames.LoadSkills, new LoadSkillsContextBuilder()),
        new(CommandNames.DeliverFindings, new DeliverFindingsContextBuilder()),
    ];

    private readonly CommandContextFactory _sut = new(Builders);

    [Fact]
    public void Create_FetchTicketCommand_ReturnsFetchTicketContext()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("123"));

        var result = _sut.Create(PipelineCommand.Simple(CommandNames.FetchTicket), project, pipeline);

        result.Should().BeOfType<FetchTicketContext>();
        var ctx = (FetchTicketContext)result;
        ctx.TicketId.Should().NotBeNull();
        ctx.TicketId!.Value.Should().Be("123");
    }

    [Fact]
    public void Create_CheckoutSourceCommand_CreatesBranchFromTicket()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("456"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, project.Repos);

        var result = _sut.Create(PipelineCommand.Simple(CommandNames.CheckoutSource), project, pipeline);

        result.Should().BeOfType<CheckoutSourceContext>();
        var ctx = (CheckoutSourceContext)result;
        ctx.Branch!.Name.Value.Should().Be("agent-smith/456");
        ctx.Branch.ComposedFromTicket.Should().BeTrue();
    }

    [Fact]
    public void Create_UnknownCommand_ThrowsConfigurationException()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();

        var act = () => _sut.Create(PipelineCommand.Simple("UnknownCommand"), project, pipeline);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unknown command*");
    }

    private static ResolvedProject CreateProjectConfig()
    {
        return new ResolvedProject
        {
            Repos = new[] { new RepoConnection { Type = RepoType.Local, Path = "/tmp" } },
            Tracker = new TrackerConnection { Type = TrackerType.GitHub, Url = "https://github.com/test/repo" },
            Agent = new AgentConfig { Type = "claude", Model = "sonnet" },
            Pipeline = "fix-bug",
            CodingPrinciplesPath = "config/principles.md"
        };
    }
}
