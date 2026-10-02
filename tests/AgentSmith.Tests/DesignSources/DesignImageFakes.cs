using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.ToolImages;
using AgentSmith.Tests.ToolImages;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ac: Figma's images endpoint as documented — {err, images: {id: url|null}} with
/// the url on the storage host observed in practice — and deposits to hand design_read.
/// </summary>
internal static class DesignImageFakes
{
    public const string ImageUrl = "https://figma-alpha-api.s3.us-west-2.amazonaws.com/images/0c1d-render-7f7ac";

    public static readonly byte[] Png = ToolImageLoopFixture.Png(720, 112);

    public static string Images(string? url = ImageUrl) =>
        "{\"err\":null,\"status\":200,\"images\":{\"1:2\":" + (url is null ? "null" : $"\"{url}\"") + "}}";

    /// <summary>A handler whose images call names <see cref="ImageUrl"/> and whose download is <paramref name="body"/>.</summary>
    public static FakeFigmaHandler Exporting(byte[]? body = null)
    {
        var handler = FakeFigmaHandler.Answering();
        handler.ImageResponses.Enqueue(() => FigmaFakes.Json(Images()));
        handler.DownloadResponses.Enqueue(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body ?? Png),
        });
        return handler;
    }

    /// <summary>The real deposit outside any loop: it refuses, naming why.</summary>
    public static IToolImageDeposit NoLoopDeposit() =>
        new AsyncLocalToolImageDeposit(new ToolImageRule(new ImageDimensionReader()), new ToolImageLoopFrames());

    /// <summary>Takes or refuses every image and keeps what it was handed.</summary>
    public sealed class RecordingDeposit(string? refusal = null) : IToolImageDeposit
    {
        public List<ToolImage> Images { get; } = [];

        public ToolImageDepositResult Deposit(ToolImage image)
        {
            Images.Add(image);
            return refusal is null ? ToolImageDepositResult.Accepted : ToolImageDepositResult.Refused(refusal);
        }
    }
}
