using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-11-05-862ea: the conversation's work picture as the page shows it under the heading — kind and size as
/// camelCase strings (rebuild, oneSpec), the truth as stated.
/// </summary>
public sealed record WorkPictureView(string Kind, string Truth, string Size)
{
    public static WorkPictureView? Of(WorkPicture? picture) => picture is null
        ? null
        : new(JsonNamingPolicy.CamelCase.ConvertName(picture.Kind.ToString()), picture.Truth,
            JsonNamingPolicy.CamelCase.ConvertName(picture.Size.ToString()));
}
