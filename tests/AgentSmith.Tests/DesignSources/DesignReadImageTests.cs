using AgentSmith.Application.Services.Design;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.ToolImages;
using AgentSmith.Tests.ToolImages;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ac: design_read hands its render to the shared tool-image deposit of
/// 2026-10-01-283dd and says in its own text whether a picture follows — over the real client
/// and a fake Figma built from the documented contract.
/// </summary>
public sealed class DesignReadImageTests
{
    private static readonly DesignSource Source = new("brand", DesignSourceVendor.Figma, FigmaFakes.SecretName);

    [Fact]
    public async Task DesignRead_RenderedNode_DepositsOnePngThroughTheSharedPath()
    {
        var handler = DesignImageFakes.Exporting();
        var deposit = new DesignImageFakes.RecordingDeposit();

        var output = await Host(handler, deposit).DesignRead(FigmaFakes.Link);

        var image = deposit.Images.Should().ContainSingle().Subject;
        image.MediaType.Should().Be("image/png");
        image.Bytes.Should().Equal(DesignImageFakes.Png);
        image.Caption.Should().Be("Figma node 1:2 of file AbCdEf123456, version 4242, PNG at scale 2");
        output.Should().Contain("rendered image: a PNG of node 1:2 (scale 2) follows this result")
            .And.Contain("FRAME \"Pay row\" [1:2] 360x56");
        handler.Requests.Single(r => r.RequestUri!.AbsolutePath.StartsWith("/v1/images/")).RequestUri!.Query
            .Should().Contain("scale=2&").And.Contain("&version=4242", "the render is of the version the summary read");
    }

    [Fact]
    public async Task DesignRead_DepositRefused_RepeatsTheReason()
    {
        var output = await Host(DesignImageFakes.Exporting(), new DesignImageFakes.RecordingDeposit("the image is empty"))
            .DesignRead(FigmaFakes.Link);

        output.Should().Contain("rendered image: not shown — the image is empty");
    }

    [Fact]
    public async Task DesignRead_OutsideAToolLoop_RepeatsTheDepositsRefusal()
    {
        var output = await Host(DesignImageFakes.Exporting(), DesignImageFakes.NoLoopDeposit()).DesignRead(FigmaFakes.Link);

        output.Should().Contain($"rendered image: not shown — {AsyncLocalToolImageDeposit.NoLoopRefusal}");
    }

    [Fact]
    public async Task DesignRead_RenderFails_StatesItAndKeepsTheSummary()
    {
        var handler = FakeFigmaHandler.Answering();
        handler.ImageResponses.Enqueue(() => FigmaFakes.Json(DesignImageFakes.Images(url: null)));
        var deposit = new DesignImageFakes.RecordingDeposit();

        var output = await Host(handler, deposit).DesignRead(FigmaFakes.Link);

        deposit.Images.Should().BeEmpty();
        output.Should().Contain("rendered image: none — unknown: Figma rendered no image for this node")
            .And.Contain("FRAME \"Pay row\"").And.Contain("collection \"Tokens\"");
    }

    [Fact]
    public async Task DesignRead_WithRender_OutputNeverContainsTokenOrUrl()
    {
        var output = await Host(DesignImageFakes.Exporting(), new DesignImageFakes.RecordingDeposit()).DesignRead(FigmaFakes.Link);

        output.Should().NotContain(FigmaFakes.Token).And.NotContain("amazonaws");
    }

    [Fact]
    public async Task DesignRead_InAScriptedLoop_ThePngReachesTheNextRequestAfterTheToolResult()
    {
        var fixture = new ToolImageLoopFixture();
        var chat = new ScriptedToolChat(["design_read"]);
        fixture.WithProvider("stub", chat);
        var host = Host(DesignImageFakes.Exporting(), fixture.Deposit);
        var tool = AIFunctionFactory.Create(() => host.DesignRead(FigmaFakes.Link, null, null, null, null, default), "design_read");

        await fixture.Loop("stub").GetResponseAsync(
            [new ChatMessage(ChatRole.User, "build the pay row")], new ChatOptions { Tools = [tool] });

        var next = chat.Requests[1];
        var toolIndex = next.FindIndex(m => m.Role == ChatRole.Tool);
        next[toolIndex].Contents.OfType<FunctionResultContent>().Single().Result!.ToString()
            .Should().Contain("rendered image: a PNG of node 1:2");
        next[toolIndex + 1].Contents.OfType<DataContent>().Should().ContainSingle()
            .Which.Data.ToArray().Should().Equal(DesignImageFakes.Png);
        next[toolIndex + 1].Text.Should().Contain("design_read").And.Contain("Figma node 1:2");
    }

    private static DesignReadToolHost Host(FakeFigmaHandler handler, IToolImageDeposit deposit)
    {
        var figma = FigmaFakes.Client(handler, new InstantClock());
        return new DesignReadToolHost(figma, [Source], render: new DesignNodeRender(figma, deposit));
    }
}
