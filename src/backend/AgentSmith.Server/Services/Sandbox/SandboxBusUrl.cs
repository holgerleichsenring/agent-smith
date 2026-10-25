using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7e: the URL a sandbox with a per-job user is handed: the server's
/// endpoints and connection options, the job's user and password, and a blank tiebreaker
/// and config channel, so the client asks for no key or channel the ACL denies.
/// </summary>
public static class SandboxBusUrl
{
    public static string Compose(string serverUrl, string user, string password)
    {
        var options = ConfigurationOptions.Parse(serverUrl);
        options.User = user;
        options.Password = password;
        // ToString leaves a blank value out, which the client reads as its default; the
        // explicit keys go last, so they are the ones a parse keeps.
        return options.ToString(includePassword: true) + ",tiebreaker=,configChannel=";
    }
}
