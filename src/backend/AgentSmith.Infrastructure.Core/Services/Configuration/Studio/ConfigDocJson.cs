using System.Text.Json;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: the serializer options for config entity docs. Both directions of the
/// store use the SAME options so a doc round-trips byte-for-byte back into the raw
/// model. Case-insensitive so a hand-edited or imported doc still binds.
/// </summary>
internal static class ConfigDocJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
