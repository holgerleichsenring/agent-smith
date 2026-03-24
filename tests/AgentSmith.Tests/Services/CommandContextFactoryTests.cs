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
        new(CommandNames.LoadDomainRules, new LoadDomainRulesContextBuilder()),
        new(CommandNames.LoadCodingPrinciples, new LoadDomainRulesContextBuilder()),
        new(CommandNames.LoadContext, new LoadContextContextBuilder()),
        new(CommandNames.LoadCodeMap, new LoadCodeMapContextBuilder()),
        new(CommandNames.BootstrapProject, new BootstrapProjectContextBuilder()),
        new(CommandNames.AnalyzeCode, new AnalyzeCodeContextBuilder()),
        new(CommandNames.GeneratePlan, new GeneratePlanContextBuilder()),
        new(CommandNames.Approval, new ApprovalContextBuilder()),
        new(CommandNames.AgenticExecute, new AgenticExecuteContextBuilder()),
        new(CommandNames.Test, new TestContextBuilder()),
        new(CommandNames.WriteRunResult, new WriteRunResultContextBuilder()),
        new(CommandNames.CommitAndPR, new CommitAndPRContextBuilder()),
        new(CommandNames.InitCommit, new InitCommitContextBuilder()),
        new(CommandNames.Triage, new TriageContextBuilder()),
        new(CommandNames.SecurityTriage, new SecurityTriageContextBuilder()),
        new(CommandNames.SwitchSkill, new SwitchSkillContextBuilder()),
        new(CommandNames.SkillRound, new SkillRoundContextBuilder()),
        new(CommandNames.SecuritySkillRound, new SecuritySkillRoundContextBuilder()),
        new(CommandNames.ConvergenceCheck, new ConvergenceCheckContextBuilder()),
        new(CommandNames.GenerateTests, new GenerateTestsContextBuilder()),
        new(CommandNames.GenerateDocs, new GenerateDocsContextBuilder()),
        new(CommandNames.CompileDiscussion, new CompileDiscussionContextBuilder()),
        new(CommandNames.LoadSwagger, new LoadSwaggerContextBuilder()),
        new(CommandNames.SpawnNuclei, new SpawnNucleiContextBuilder()),
        new(CommandNames.ApiSecurityTriage, new ApiSecurityTriageContextBuilder()),
        new(CommandNames.ApiSecuritySkillRound, new ApiSecuritySkillRoundContextBuilder()),
        new(CommandNames.CompileFindings, new CompileFindingsContextBuilder()),
        new(CommandNames.LoadSkills, new LoadSkillsContextBuilder()),
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
        ctx.TicketId.Value.Should().Be("123");
    }

    [Fact]
    public void Create_CheckoutSourceCommand_CreatesBranchFromTicket()
    {
        var project = CreateProjectConfig();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("456"));

        var result = _sut.Create(PipelineCommand.Simple(CommandNames.CheckoutSource), project, pipeline);

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

        var result = _sut.Create(PipelineCommand.Simple(CommandNames.GeneratePlan), project, pipeline);

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

        var act = () => _sut.Create(PipelineCommand.Simple("UnknownCommand"), project, pipeline);

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
