using System.ClientModel.Primitives;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0477: the minimum a <c>ClientResultException</c> needs to carry a status. p0493 gave it
/// headers too, because the interval a rate-limited server names is sent in one and the
/// Azure and OpenAI path never read it.
/// </summary>
internal sealed class FakeResponse(int status, params (string Name, string Value)[] headers) : PipelineResponse
{
    public override int Status => status;
    public override string ReasonPhrase => "test";
    public override Stream? ContentStream { get => null; set { } }
    public override BinaryData Content => BinaryData.FromString(string.Empty);
    protected override PipelineResponseHeaders HeadersCore { get; } = new FakeHeaders(headers);
    public override BinaryData BufferContent(CancellationToken ct = default) => Content;
    public override ValueTask<BinaryData> BufferContentAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(Content);
    public override void Dispose() { }

    private sealed class FakeHeaders(params (string Name, string Value)[] headers) : PipelineResponseHeaders
    {
        private readonly Dictionary<string, string> _byName =
            headers.ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);

        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _byName.GetEnumerator();

        public override bool TryGetValue(string name, out string? value) =>
            _byName.TryGetValue(name, out value);

        public override bool TryGetValues(string name, out IEnumerable<string>? values)
        {
            var found = _byName.TryGetValue(name, out var value);
            values = found ? [value!] : null;
            return found;
        }
    }
}
