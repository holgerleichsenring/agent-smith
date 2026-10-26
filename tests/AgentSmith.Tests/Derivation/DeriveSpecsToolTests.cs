using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Derivation;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Derivation;

/// <summary>2026-10-26-bdd0g: derive_specs in a bound conversation — the port the turn is seeded with, the tool over
/// it, and the sentence the conversation is told.</summary>
public sealed class DeriveSpecsToolTests
{
    private readonly Mock<ISpecDerivationStart> _start = new();

    [Fact]
    public async Task DeriveSpecsTool_Branch_StartsAndSaysStarted()
    {
        _start.Setup(s => s.StartAsync(false, "derive them now", It.IsAny<CancellationToken>()))
            .ReturnsAsync("started as run r-9; the derived specs appear on the ticket page when it ends");
        var tool = new DeriveSpecsToolHost(_start.Object).GetTools(null, null).Single();

        var answer = await tool.InvokeAsync(new AIFunctionArguments { ["mode"] = "branch", ["reason"] = "derive them now" });

        tool.Name.Should().Be(DeriveSpecsToolHost.Name);
        answer!.ToString().Should().Contain("started as run r-9");
    }

    [Fact]
    public async Task DeriveSpecsTool_Preview_AsksForAPreview()
    {
        var tool = new DeriveSpecsToolHost(_start.Object).GetTools(null, null).Single();

        await tool.InvokeAsync(new AIFunctionArguments { ["mode"] = "preview", ["reason"] = "just show me" });

        _start.Verify(s => s.StartAsync(true, "just show me", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeriveSpecsTool_Refused_SaysWhy()
    {
        var services = new ServiceCollection();
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());
        services.AddSingleton(new SpecDerivationLauncher(loader.Object, new ServerContext("agentsmith.yml"),
            null!, null!, null!, null!, null!, null!, TimeProvider.System, NullLogger<SpecDerivationLauncher>.Instance));
        var derivation = new ConversationSpecDerivation(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new ResolvedProject { Name = "gone" },
            "42", "s-1", "ops-1", NullLogger<ConversationSpecDerivation>.Instance);

        var answer = await derivation.StartAsync(false, "derive", default);

        answer.Should().Be("Not started: Project 'gone' is not configured.");
    }

    [Fact]
    public void BoundTicket_PosterWithoutRunsControl_GetsNoDeriveSpecs()
    {
        var factory = new TicketActToolsFactory(Handlers.MasterHandlerFixture.Verification());
        var seeded = Context();
        seeded.Pipeline.Set(ContextKeys.SpecDialogDerivationStart, _start.Object);

        factory.Tools(seeded).Select(t => t.Name).Should().Contain(DeriveSpecsToolHost.Name);
        factory.Tools(Context()).Select(t => t.Name).Should().NotContain(DeriveSpecsToolHost.Name,
            "a turn whose poster may not start runs is seeded with no port, so it is offered no tool");
    }

    private static AgenticMasterContext Context() => new(
        PipelinePresets.TicketStewardMaster, new Repository(new BranchName("main"), "/tmp"), string.Empty,
        new AgentConfig { Type = "claude" }, new PipelineContext());
}
