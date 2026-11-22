using System.ClientModel.Primitives;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: a pipeline step on the Azure and OpenAI client options that hands every response's headers to
/// <see cref="ProviderQuotaHeaders"/>. It changes nothing in the request or the response.
/// </summary>
internal sealed class ProviderQuotaHeaderPolicy(ProviderQuotaHeaders headers) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessNext(message, pipeline, currentIndex);
        Record(message);
    }

    public override async ValueTask ProcessAsync(
        PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await ProcessNextAsync(message, pipeline, currentIndex);
        Record(message);
    }

    private void Record(PipelineMessage message)
    {
        if (message.Response is { } response) headers.Record(response.Headers);
    }
}
