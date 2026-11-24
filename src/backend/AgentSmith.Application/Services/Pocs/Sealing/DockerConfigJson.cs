using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>2026-11-23-933dg: the .dockerconfigjson of a pull secret — one auths entry for the login server with the
/// pull user and token (and their base64 'auth'). Returned in a redacting holder: it carries the token, and nothing
/// masks it in this form, so it goes to the sealer only — never into a log or an event.</summary>
public static class DockerConfigJson
{
    public static SecretInput For(string loginServer, string user, SecretInput token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token.Reveal()}"));
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["auths"] = new Dictionary<string, object>
            {
                [loginServer] = new Dictionary<string, string> { ["username"] = user, ["password"] = token.Reveal(), ["auth"] = auth },
            },
        });
        return new SecretInput(json);
    }
}
