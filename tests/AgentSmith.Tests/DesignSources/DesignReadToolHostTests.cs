using System.Net;
using System.Text;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ab: design_read end to end over the real client and a fake Figma API built from
/// the documented shapes — the summary it returns, each failure it reports, and that the token is
/// in none of it.
/// </summary>
public sealed class DesignReadToolHostTests
{
    private static readonly DesignSource Source = new("brand", DesignSourceVendor.Figma, FigmaFakes.SecretName);

    [Fact]
    public async Task DesignRead_RecordedNodesResponse_SummarisesVersionLayoutColoursAndText()
    {
        var output = await Host(FakeFigmaHandler.Answering()).DesignRead(FigmaFakes.Link);

        output.Should().Contain("source: brand").And.Contain("file: Checkout").And.Contain("version: 4242")
            .And.Contain("last modified: 2026-09-30T10:00:00Z");
        output.Should().Contain("FRAME \"Pay row\" [1:2] 360x56")
            .And.Contain("auto-layout horizontal gap 8 pad 12 16 12 16 align SPACE_BETWEEN/CENTER")
            .And.Contain("fill #FFFFFF").And.Contain("radius 8").And.Contain("variables itemSpacing=space/sm");
        output.Should().Contain("text \"Pay now\" Inter 600 16/24").And.Contain("fill #0055FF")
            .And.Contain("styles text=Heading/H3");
        output.Should().Contain("instance of \"Button/Primary\"").And.Contain("stroke #00000080 1px");
        output.Should().NotContain("Hidden", "a hidden layer is not part of what is built");
        output.Should().Contain("collection \"Tokens\" (modes: Light, Dark)")
            .And.Contain("color/brand: Light #0055FF · Dark -> space/sm");
    }

    [Fact]
    public async Task DesignRead_Forbidden_ReportsForbiddenKind_WithoutBody()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Status(HttpStatusCode.Forbidden));

        var output = await Host(handler).DesignRead(FigmaFakes.Link);

        output.Should().StartWith("design_read failed: forbidden").And.NotContain("FIGMA-BODY-MARKER");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "not_found")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "unknown")]
    public async Task DesignRead_EachFailure_ReportsItsKind(HttpStatusCode status, string kind)
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => status == HttpStatusCode.TooManyRequests
            ? FigmaFakes.RateLimited(90) : FigmaFakes.Status(status));

        var output = await Host(handler).DesignRead(FigmaFakes.Link);

        output.Should().StartWith($"design_read failed: {kind}").And.NotContain("FIGMA-BODY-MARKER");
        if (status == HttpStatusCode.TooManyRequests) output.Should().Contain("retry after 90s");
    }

    [Fact]
    public async Task DesignRead_VariablesForbidden_SaysUnavailableAndKeepsNodeValues()
    {
        var output = await Host(FakeFigmaHandler.Answering(HttpStatusCode.Forbidden)).DesignRead(FigmaFakes.Link);

        output.Should().Contain("variables: unavailable (forbidden").And.Contain("file_variables:read");
        output.Should().Contain("fill #0055FF", "the values set on the nodes stand on their own");
        output.Should().Contain("itemSpacing=VariableID:1:5", "an unread variable is shown by id, never given a name");
        output.Should().NotContain("space/sm");
    }

    [Fact]
    public async Task DesignRead_OverBudget_StatesTruncation()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Json(ManyNodes(2_000)));
        handler.VariableResponses.Enqueue(() => FigmaFakes.Json(FigmaFakes.Variables));

        var output = await Host(handler).DesignRead(FigmaFakes.Link);

        output.Should().MatchRegex(@"\[truncated: \d+ of 2001 nodes not shown — budget 16000 characters");
        output.Length.Should().BeLessThan(16_000 + 7_000);
    }

    [Fact]
    public async Task DesignRead_OutputNeverContainsToken()
    {
        var outputs = new[]
        {
            await Host(FakeFigmaHandler.Answering()).DesignRead(FigmaFakes.Link),
            await Host(FakeFigmaHandler.Answering(HttpStatusCode.Forbidden)).DesignRead(FigmaFakes.Link),
            await Host(new FakeFigmaHandler()).DesignRead(FigmaFakes.Link),
        };

        outputs.Should().OnlyContain(o => !o.Contains(FigmaFakes.Token));
    }

    [Theory]
    [InlineData("https://www.figma.com.evil.test/design/AbCdEf123456/x?node-id=1-2", "not a readable Figma link")]
    [InlineData("https://www.figma.com/design/AbCdEf123456/Checkout", "names no node")]
    public async Task DesignRead_UnreadableLink_IsRefusedWithoutAFetch(string url, string reason)
    {
        var handler = FakeFigmaHandler.Answering();

        (await Host(handler).DesignRead(url)).Should().Contain(reason);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesignRead_SeveralSources_NeedsOneNamed()
    {
        var handler = FakeFigmaHandler.Answering();
        var other = new DesignSource("marketing", DesignSourceVendor.Figma, "other-token");
        var host = new DesignReadToolHost(FigmaFakes.Client(handler), [other, Source]);

        (await host.DesignRead(FigmaFakes.Link)).Should().Contain("one of marketing, brand");
        (await host.DesignRead(FigmaFakes.Link, source: "brand")).Should().Contain("version: 4242");
    }

    [Fact]
    public async Task DesignRead_SecondReadOfAFile_ReadsVariablesOnce()
    {
        var handler = FakeFigmaHandler.Answering();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Json(FigmaFakes.Nodes));
        var host = Host(handler);

        await host.DesignRead(FigmaFakes.Link);
        await host.DesignRead(FigmaFakes.Link);

        handler.Requests.Count(r => r.RequestUri!.AbsolutePath.EndsWith("/variables/local")).Should().Be(1);
    }

    [Fact]
    public void GetTools_OffersDesignRead() =>
        Host(new FakeFigmaHandler()).GetTools(null, null).Select(t => t.Name).Should().Equal("design_read");

    private static DesignReadToolHost Host(FakeFigmaHandler handler) =>
        new(FigmaFakes.Client(handler, new InstantClock()), [Source]);

    private static string ManyNodes(int count)
    {
        var children = new StringBuilder();
        for (var i = 0; i < count; i++)
            children.Append(i == 0 ? "" : ",").Append(
                $"{{\"id\":\"2:{i}\",\"name\":\"Row {i}\",\"type\":\"TEXT\",\"characters\":\"Line {i}\"}}");
        return "{\"name\":\"Big\",\"version\":\"1\",\"lastModified\":\"2026-09-30T10:00:00Z\",\"nodes\":{\"1:2\":"
            + $"{{\"document\":{{\"id\":\"1:2\",\"name\":\"List\",\"type\":\"FRAME\",\"children\":[{children}]}}}}}}}}";
    }
}
