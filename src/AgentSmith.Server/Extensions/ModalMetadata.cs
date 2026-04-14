namespace AgentSmith.Server.Extensions;

internal sealed class ModalMetadata
{
    [System.Text.Json.Serialization.JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("selected_project")]
    public string? SelectedProject { get; set; }
}
