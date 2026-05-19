using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Shrinks an OpenAPI / swagger.json spec when its <see cref="SwaggerSpec.RawJson"/>
/// risks crowding out the rest of the LLM input window.
///
/// Activation is threshold-gated: specs under <see cref="SizeThresholdChars"/> are
/// returned unchanged (pass-through). Above the threshold, the implementation strips
/// example payloads, truncates verbose descriptions, and drops component schemas that
/// no path references (transitively). The path / method / security-scheme shape stays
/// intact so skills observe the same API surface either way.
///
/// Skills that need the original spec verbatim (e.g. payload-fuzz scanners) read it
/// from <c>ContextKeys.SwaggerSpecFull</c>; the compressed form is always the default
/// <c>ContextKeys.SwaggerSpec</c>.
/// </summary>
public interface ISwaggerSpecCompressor
{
    /// <summary>
    /// Size threshold (raw JSON chars) above which compression kicks in.
    /// AuthPort's real-world spec measured at 291k chars in the run that motivated this work.
    /// </summary>
    int SizeThresholdChars { get; }

    /// <summary>
    /// Returns a possibly-compressed copy of <paramref name="spec"/>.
    /// Specs at or below <see cref="SizeThresholdChars"/> are returned unchanged.
    /// </summary>
    SwaggerSpec Compress(SwaggerSpec spec);
}
