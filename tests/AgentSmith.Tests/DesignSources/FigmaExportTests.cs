using System.Net;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Infrastructure.Services.Providers.Design;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ac: the PNG export against a fake handler built from Figma's documented images
/// contract — where the token goes (the API only), which urls are fetched, and what is refused.
/// </summary>
public sealed class FigmaExportTests
{
    [Fact]
    public async Task FigmaExport_DownloadRequest_CarriesNoTokenHeader()
    {
        var handler = DesignImageFakes.Exporting();

        var export = await Export(handler, scale: 0.5, version: "4242");

        export.Png.Should().Equal(DesignImageFakes.Png);
        handler.Requests.Should().HaveCount(2);
        var images = handler.Requests[0];
        images.RequestUri!.Host.Should().Be("api.figma.com");
        images.RequestUri.PathAndQuery.Should()
            .Be("/v1/images/AbCdEf123456?ids=1%3A2&format=png&scale=0.5&use_absolute_bounds=true&version=4242");
        images.Headers.GetValues(FigmaClient.TokenHeader).Should().Equal(FigmaFakes.Token);
        var download = handler.Requests[1];
        download.RequestUri!.AbsoluteUri.Should().Be(DesignImageFakes.ImageUrl);
        download.Headers.Should().BeEmpty("the storage host must receive neither the token nor any other header");
    }

    [Fact]
    public async Task FigmaExport_NullImageEntry_IsAStatedRenderFailure()
    {
        var handler = FakeFigmaHandler.Answering();
        handler.ImageResponses.Enqueue(() => FigmaFakes.Json(DesignImageFakes.Images(url: null)));

        var export = await Export(handler);

        export.Png.Should().BeNull();
        export.Failure!.Detail.Should().Contain("rendered no image");
        handler.Requests.Should().ContainSingle("nothing is downloaded when Figma rendered nothing");
    }

    [Fact]
    public async Task FigmaExport_NotPng_IsRefused()
    {
        var export = await Export(DesignImageFakes.Exporting("<html>denied</html>"u8.ToArray()));

        export.Failure!.Detail.Should().Be("the downloaded image is not a PNG");
    }

    [Fact]
    public async Task FigmaExport_ImagesCallForbidden_ReportsTheKindAndDownloadsNothing()
    {
        var handler = new FakeFigmaHandler();
        handler.ImageResponses.Enqueue(() => FigmaFakes.Status(HttpStatusCode.Forbidden));

        var export = await Export(handler);

        export.Failure!.Kind.Should().Be(FigmaReadFailureKind.Forbidden);
        export.Failure.Detail.Should().NotContain("FIGMA-BODY-MARKER");
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("http://figma-alpha-api.s3.us-west-2.amazonaws.com/images/x")]
    [InlineData("https://attacker.example/images/x")]
    [InlineData("https://figma-alpha-api.s3.us-west-2.amazonaws.com.attacker.example/x")]
    [InlineData("https://user@figma-alpha-api.s3.us-west-2.amazonaws.com/images/x")]
    [InlineData("https://figma-alpha-api.s3.us-west-2.amazonaws.com:8443/images/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    public async Task FigmaExport_UrlOffTheImageHost_IsNotFetched(string url)
    {
        var handler = new FakeFigmaHandler();

        var export = await FigmaFakes.Downloader(handler).DownloadPngAsync(url, CancellationToken.None);

        export.Failure!.Detail.Should().Contain("not https on Figma's image host");
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FigmaExport_OverTheByteCap_IsRefused(bool lengthAnnounced)
    {
        var body = new byte[FigmaImageDownloader.MaxBytes + 1];
        DesignImageFakes.Png.CopyTo(body, 0);
        var handler = new FakeFigmaHandler();
        handler.DownloadResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = lengthAnnounced ? new ByteArrayContent(body) : new StreamContent(new UnseekableStream(body)),
        });

        var export = await FigmaFakes.Downloader(handler).DownloadPngAsync(DesignImageFakes.ImageUrl, CancellationToken.None);

        export.Failure!.Detail.Should().Contain("over the 5242880-byte limit");
    }

    [Fact]
    public async Task FigmaExport_Redirect_IsAFailureNotAHop()
    {
        var handler = new FakeFigmaHandler();
        handler.DownloadResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://attacker.example/") },
        });

        var export = await FigmaFakes.Downloader(handler).DownloadPngAsync(DesignImageFakes.ImageUrl, CancellationToken.None);

        export.Failure!.Detail.Should().Be("the image download answered HTTP 302");
        handler.Requests.Should().ContainSingle();
    }

    private static Task<FigmaExportResult> Export(FakeFigmaHandler handler, double scale = 1, string? version = null) =>
        FigmaFakes.Client(handler, new InstantClock())
            .ExportPngAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", scale, version, CancellationToken.None);

    private sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
