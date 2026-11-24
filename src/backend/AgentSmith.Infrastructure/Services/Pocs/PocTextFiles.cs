using System.Text;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>2026-11-23-933dc: which of a PoC's files are text — strict UTF-8 with no NUL — read in full from memory;
/// the rest are binary and only listed.</summary>
public sealed class PocTextFiles
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    public string? TextOf(ReferenceSetFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (Array.IndexOf(file.Content, (byte)0) >= 0) return null;
        try { return StrictUtf8.GetString(file.Content); }
        catch (DecoderFallbackException) { return null; }
    }
}
