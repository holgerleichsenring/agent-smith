using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: the poc-extend run's step — an owner twice, a third time only with an admin's route call, a
/// deleted PoC never; the extension adds extend_days to the later of now and the expiry and is published for its audit.</summary>
public sealed class PocExtendHandlerTests
{
    private readonly FakeLifetimeStore _store = new();
    private readonly PocClock _clock = new(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly List<PocActedEvent> _events = [];

    private PocExtendHandler Handler()
    {
        var config = new AgentSmithConfig { Pocs = PocsConfigTests.Sound() };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => _events.Add((PocActedEvent)e)).Returns(Task.CompletedTask);
        return new PocExtendHandler(_store, source, new PocLifetime(), new PocActedPublisher(events.Object, _clock), _clock,
            NullLogger<PocExtendHandler>.Instance);
    }

    private void Poc(int extensions, string state = PocStates.Ready, int createdDaysAgo = 80) =>
        _store.Subjects.Add(new PocLifetimeSubject(7, "shop", "main", state, 1, 2,
            new PocLifetimeFacts(_clock.Now.AddDays(-createdDaysAgo), null, null, extensions), null, false, null));

    private Task<AgentSmith.Domain.Models.CommandResult> ExtendAsync(bool mayExceed = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "extend-1");
        pipeline.Set(ContextKeys.PocId, "7");
        pipeline.Set(ContextKeys.PocActor, "ann-person");
        pipeline.Set(ContextKeys.PocMayExceed, mayExceed ? "true" : "false");
        return Handler().ExecuteAsync(new PocExtendContext(pipeline, new ResolvedProject { Name = "poc-ann" }, PocSeeds.From(pipeline)), default);
    }

    [Fact]
    public async Task Extend_AddsExtendDaysToTheExpiry_AndPublishesExtended()
    {
        Poc(0);

        var result = await ExtendAsync();

        result.IsSuccess.Should().BeTrue(result.Message);
        _store.Extended.Should().ContainSingle().Which.ExpiresAtTicks.Should().Be(_clock.Now.AddDays(10 + 90).UtcTicks);
        _events.Should().ContainSingle().Which.Should().Match<PocActedEvent>(e => e.AuditAct == PocActs.Extended
            && e.Act == PocActs.Extended && e.Reason!.StartsWith("extension 1 of 2") && e.Actor == "ann-person");
    }

    [Fact]
    public async Task Extend_AnExpiredPoc_CountsFromNow()
    {
        Poc(0, PocStates.Stopped, createdDaysAgo: 120);

        (await ExtendAsync()).IsSuccess.Should().BeTrue();

        _store.Extended.Single().ExpiresAtTicks.Should().Be(_clock.Now.AddDays(90).UtcTicks);
    }

    [Fact]
    public async Task Extend_AThirdOwnerExtension_IsRefusedNamingTheKey_AnAdminsPasses()
    {
        Poc(2);

        var owner = await ExtendAsync();
        var admin = await ExtendAsync(mayExceed: true);

        owner.IsSuccess.Should().BeFalse();
        owner.Message.Should().Contain(PocLifetimeConfig.MaxOwnerExtensionsKey).And.Contain("at most 2 times").And.Contain("a PoC is not a product");
        admin.IsSuccess.Should().BeTrue(admin.Message);
        _events.Single().Reason.Should().Contain($"past {PocLifetimeConfig.MaxOwnerExtensionsKey} (2) by an admin");
    }

    [Fact]
    public async Task Extend_ADeletedPoc_IsRefused()
    {
        Poc(0, PocStates.Deleted);

        var result = await ExtendAsync(mayExceed: true);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("'shop' is deleted");
        _store.Extended.Should().BeEmpty();
    }
}
