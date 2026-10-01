using System.Text.Json;
using AgentSmith.Application.Services.Design;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ae: what a run records of each design read, and what a read given the version a
/// ticket cites says about whether the design moved since.
/// </summary>
public sealed class DesignVersionRecordTests
{
    private static readonly DesignSource Source = new("brand", DesignSourceVendor.Figma, FigmaFakes.SecretName);

    [Fact]
    public async Task DesignRead_InARun_PublishesDesignReadEventWithVersion()
    {
        var events = EventTestStubs.Recording();
        var pipeline = RunPipeline();
        pipeline.Set(ContextKeys.RunId, "run-7f7ae");

        await Factory(FakeFigmaHandler.Answering(), events).Create(pipeline)!.DesignRead(FigmaFakes.Link);

        var read = events.Events.Should().ContainSingle().Which.Should().BeOfType<DesignReadEvent>().Subject;
        read.RunId.Should().Be("run-7f7ae");
        read.Type.Should().Be(EventType.DesignRead);
        (read.Source, read.FileKey, read.NodeId, read.Version, read.LastModified)
            .Should().Be(("brand", "AbCdEf123456", "1:2", "4242", "2026-09-30T10:00:00Z"));
    }

    [Fact]
    public async Task DesignRead_WithoutRunId_PublishesNothing()
    {
        var events = EventTestStubs.Recording();
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, [Source]);

        var output = await Factory(FakeFigmaHandler.Answering(), events).Create(pipeline)!.DesignRead(FigmaFakes.Link);

        output.Should().Contain("version: 4242");
        events.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task DesignRead_FailedRead_PublishesNothing()
    {
        var events = EventTestStubs.Recording();
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Status(System.Net.HttpStatusCode.NotFound));
        var pipeline = RunPipeline();
        pipeline.Set(ContextKeys.RunId, "run-1");

        await Factory(handler, events).Create(pipeline)!.DesignRead(FigmaFakes.Link);

        events.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task DesignRead_ExpectedVersionDiffers_OpensWithDesignMoved()
    {
        var handler = FakeFigmaHandler.Answering();

        var output = await Host(handler).DesignRead(FigmaFakes.Link, expected_version: "4100");

        output.Should().StartWith("design moved: 4100 -> 4242 (2026-09-30T10:00:00Z)").And.Contain("version: 4242");
        handler.Requests.First().RequestUri!.Query.Should().NotContain("version=");
    }

    [Fact]
    public async Task DesignRead_ExpectedVersionCurrent_SaysUnchanged() =>
        (await Host(FakeFigmaHandler.Answering()).DesignRead(FigmaFakes.Link, expected_version: "4242"))
            .Should().StartWith("design unchanged: version 4242 is current");

    [Fact]
    public async Task DesignRead_ReadVersion_PassesVersionQuery()
    {
        var handler = FakeFigmaHandler.Answering();

        var output = await Host(handler).DesignRead(FigmaFakes.Link, expected_version: "4100", read_version: true);

        handler.Requests.First().RequestUri!.Query.Should().Contain("&version=4100");
        output.Should().StartWith("reading the cited version 4100");
    }

    [Fact]
    public async Task DesignRead_ReadVersionWithoutExpected_IsRefusedWithoutAFetch()
    {
        var handler = FakeFigmaHandler.Answering();

        (await Host(handler).DesignRead(FigmaFakes.Link, read_version: true)).Should().Contain("needs expected_version");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void DesignVersionNote_NoCitedVersion_IsEmpty() =>
        DesignVersionNote.Render(null, false, JsonDocument.Parse(FigmaFakes.Nodes).RootElement).Should().BeEmpty();

    [Fact]
    public async Task DesignReadRecorder_PublisherThrows_DoesNotFailTheRead()
    {
        FigmaLink.TryParse(FigmaFakes.Link, out var link);
        var recorder = new DesignReadRecorder(new ThrowingPublisher(), "run-1", NullLogger.Instance);

        var record = () => recorder.RecordAsync("brand", link!, JsonDocument.Parse(FigmaFakes.Nodes).RootElement, CancellationToken.None);

        await record.Should().NotThrowAsync();
    }

    private static PipelineContext RunPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", DesignSources = [Source] });
        return pipeline;
    }

    private static DesignReadToolHostFactory Factory(FakeFigmaHandler handler, IEventPublisher events) =>
        new(FigmaFakes.Client(handler, new InstantClock()), DesignImageFakes.NoLoopDeposit(), events,
            NullLogger<DesignReadToolHostFactory>.Instance);

    private static DesignReadToolHost Host(FakeFigmaHandler handler) =>
        new(FigmaFakes.Client(handler, new InstantClock()), [Source]);

    private sealed class ThrowingPublisher : IEventPublisher
    {
        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("transport down");
    }
}
