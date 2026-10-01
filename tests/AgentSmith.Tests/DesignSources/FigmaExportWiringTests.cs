using AgentSmith.Contracts.Models.Design;
using AgentSmith.Infrastructure.Services.Providers.Design;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ac: the download client as the container builds it — no redirects, no default
/// headers — and a failed download that says nothing of the url it tried.
/// </summary>
public sealed class FigmaExportWiringTests
{
    [Fact]
    public void AddDesignProviders_DownloadClient_FollowsNoRedirectAndCarriesNoHeader()
    {
        using var provider = Provider();

        provider.GetRequiredService<IFigmaImageDownloader>().Should().BeOfType<FigmaImageDownloader>();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(IFigmaImageDownloader));
        while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;
        handler.Should().BeOfType<SocketsHttpHandler>().Which.AllowAutoRedirect.Should().BeFalse();
        provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IFigmaImageDownloader))
            .DefaultRequestHeaders.Should().BeEmpty();
    }

    [Fact]
    public async Task FigmaExport_ConnectionFails_ReportsUnreachableWithoutTheUrl()
    {
        var handler = new FakeFigmaHandler();
        handler.DownloadResponses.Enqueue(() => throw new HttpRequestException("reset " + DesignImageFakes.ImageUrl));

        var export = await FigmaFakes.Downloader(handler).DownloadPngAsync(DesignImageFakes.ImageUrl, CancellationToken.None);

        export.Failure!.Kind.Should().Be(FigmaReadFailureKind.Unreachable);
        export.Failure.Detail.Should().NotContain("amazonaws").And.NotContain("reset");
    }

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<AgentSmith.Contracts.Services.ISecretValues>(
                new AgentSmith.Contracts.Services.LoadedSecretValues(
                    AgentSmith.Contracts.Models.Configuration.AgentSmithConfig.Empty()));
        services.AddDesignProviders();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
