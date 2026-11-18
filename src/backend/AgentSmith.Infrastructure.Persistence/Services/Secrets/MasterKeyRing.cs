using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: the master keys of the secret store, read from where the deployment keeps them and never from
/// the database: AGENTSMITH_SECRET_MASTER_KEY (base64 of 32 bytes) or AGENTSMITH_SECRET_MASTER_KEY_FILE (a mounted
/// k8s secret), and the same pair with _PREVIOUS for reading what the key before a rotation wrapped. A key's id is an
/// HMAC of a fixed label under the key, so it identifies the key without being a check value for a guessed one.
/// </summary>
public sealed class MasterKeyRing
{
    public const string Variable = "AGENTSMITH_SECRET_MASTER_KEY";
    private const string FileSuffix = "_FILE";
    private const string PreviousSuffix = "_PREVIOUS";
    private const int KeyBytes = 32; // not a limit: AES-256 key size
    private static readonly byte[] IdLabel = Encoding.UTF8.GetBytes("agentsmith/keyid");

    public MasterKeyRing(Func<string, string?> environment)
    {
        Current = Read(environment, Variable, out var problem);
        Previous = Read(environment, Variable + PreviousSuffix, out var previousProblem);
        if (Previous is not null && Current is not null && Previous.Id == Current.Id) Previous = null;
        Problem = problem ?? previousProblem;
    }

    public MasterKey? Current { get; }

    public MasterKey? Previous { get; }

    /// <summary>What is wrong with the keys as configured, naming the variable; null when nothing is.</summary>
    public string? Problem { get; }

    public MasterKey? Find(string keyId) =>
        Current?.Id == keyId ? Current : Previous?.Id == keyId ? Previous : null;

    private static MasterKey? Read(Func<string, string?> environment, string variable, out string? problem)
    {
        problem = null;
        var text = environment(variable) ?? FileText(environment(variable + FileSuffix));
        if (string.IsNullOrWhiteSpace(text)) return null;
        var key = Decode(text.Trim());
        if (key is { Length: KeyBytes }) return new MasterKey(IdOf(key), key);
        problem = $"{variable} is set but is not the base64 of {KeyBytes} bytes; stored secret values cannot be read or written.";
        return null;
    }

    private static string? FileText(string? path) => string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : File.ReadAllText(path);

    private static byte[]? Decode(string text)
    {
        var buffer = new byte[text.Length];
        return Convert.TryFromBase64String(text, buffer, out var written) ? buffer[..written] : null;
    }

    private static string IdOf(byte[] key) => "env:" + Convert.ToHexStringLower(HMACSHA256.HashData(key, IdLabel))[..16];
}
