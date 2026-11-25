using System.Text;

namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-11-25-9992d: the one place a repository-scoped secret's container variable name is spelled. The pod carries
/// <c>sandbox.secrets.repo_env</c> bindings of every repository in every toolchain container under
/// <c>AGENTSMITH_SECRET_&lt;HEX&gt;__&lt;NAME&gt;</c>, HEX being the uppercase hex of the repository name's UTF-8 bytes,
/// so the name stays a valid Kubernetes env name whatever the repository is called and can never collide with a
/// variable a command reads. The agent drops every such variable from a command's environment and hands back, under
/// its plain NAME, only those of the repository the step runs for (<see cref="Step.SecretScope"/>).
/// </summary>
public static class ScopedSecretNames
{
    public const string Prefix = "AGENTSMITH_SECRET_";

    private const string Separator = "__";

    /// <summary>The container variable carrying <paramref name="name"/> for <paramref name="repository"/>.</summary>
    public static string For(string repository, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(repository);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return $"{Prefix}{Convert.ToHexString(Encoding.UTF8.GetBytes(repository))}{Separator}{name}";
    }

    /// <summary>True for every variable of this family, whichever repository it belongs to.</summary>
    public static bool IsScoped(string variable) => variable.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Reads a scoped variable back into its repository and plain name; false for anything else.</summary>
    public static bool TryParse(string variable, out string repository, out string name)
    {
        repository = name = string.Empty;
        if (!IsScoped(variable)) return false;
        var rest = variable[Prefix.Length..];
        var split = rest.IndexOf(Separator, StringComparison.Ordinal);
        if (split <= 0 || split + Separator.Length >= rest.Length) return false;
        try
        {
            repository = Encoding.UTF8.GetString(Convert.FromHexString(rest[..split]));
        }
        catch (FormatException)
        {
            return false;
        }
        name = rest[(split + Separator.Length)..];
        return true;
    }
}
