using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: reads a stored turn kind this release does not know as null, so a
/// transcript kept by a newer release stays readable after a rollback. A null kind is a legacy
/// turn to the proposal rule, which is the most such a turn can be trusted to say.
/// </summary>
internal sealed class TolerantTurnKindConverter : JsonConverter<SpecDialogTurnKind?>
{
    private static readonly JsonNamingPolicy Naming = JsonNamingPolicy.CamelCase;

    public override bool HandleNull => true;

    public override SpecDialogTurnKind? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }
        return Enum.TryParse<SpecDialogTurnKind>(reader.GetString(), ignoreCase: true, out var kind)
            && Enum.IsDefined(kind) ? kind : null;
    }

    public override void Write(Utf8JsonWriter writer, SpecDialogTurnKind? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Naming.ConvertName(value.Value.ToString()));
    }
}
