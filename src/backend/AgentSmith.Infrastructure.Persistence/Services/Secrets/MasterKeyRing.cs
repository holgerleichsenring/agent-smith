using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: the master keys of the secret store, read from where the deployment keeps them and never from
/// the database: AGENTSMITH_SECRET_MASTER_KEY (base64 of 32 bytes) or AGENTSMITH_SECRET_MASTER_KEY_FILE (a mounted
/// k8s secret), and the same pair with _PREVIOUS for reading what the key before a rotation wrapped. A key's id is an
/// HMAC of a fixed label under the key, so it identifies the key without being a check value for a guessed one.
/// <para>
/// 2026-11-17-f3a8e: a slot may instead hold the key wrapped — AGENTSMITH_SECRET_MASTER_KEY_WRAPPED (or _FILE, and
/// _PREVIOUS), the key encrypted RSA-OAEP-256 with the public half of the Azure Key Vault key that
/// AGENTSMITH_SECRET_KEY_VAULT_KEY names (_PREVIOUS falls back to it). The vault unwraps it once, here; the
/// deployment then holds no raw key. The id names the key's bytes, whichever form delivered them, so a key moves from
/// raw to wrapped without a re-wrap.
/// </para>
/// </summary>
public sealed class MasterKeyRing
{
    public const string Variable = "AGENTSMITH_SECRET_MASTER_KEY";
    public const string WrappedVariable = Variable + "_WRAPPED";
    public const string VaultKeyVariable = "AGENTSMITH_SECRET_KEY_VAULT_KEY";
    private const string FileSuffix = "_FILE";
    private const string PreviousSuffix = "_PREVIOUS";
    private const int KeyBytes = 32; // not a limit: AES-256 key size
    private static readonly byte[] IdLabel = Encoding.UTF8.GetBytes("agentsmith/keyid");

    private readonly Func<string, string?> _environment;
    private readonly IKeyVaultUnwrap? _vault;
    private readonly List<string> _problems = [];

    public MasterKeyRing(Func<string, string?> environment, IKeyVaultUnwrap? vault = null)
    {
        (_environment, _vault) = (environment, vault);
        (Current, Source) = Read(string.Empty);
        (Previous, _) = Read(PreviousSuffix);
        if (Previous is not null && Current is not null && Previous.Id == Current.Id) Previous = null;
        Problem = _problems.Count == 0 ? null : string.Join(" ", _problems);
    }

    public MasterKey? Current { get; }

    public MasterKey? Previous { get; }

    /// <summary>What is wrong with the keys as configured, naming the variable; null when nothing is.</summary>
    public string? Problem { get; }

    /// <summary>2026-11-17-f3a8e: where the current key came from, naming the variable and any vault key; null without one.</summary>
    public string? Source { get; }

    public MasterKey? Find(string keyId) =>
        Current?.Id == keyId ? Current : Previous?.Id == keyId ? Previous : null;

    private (MasterKey? Key, string? Source) Read(string suffix)
    {
        var raw = Text(Variable + suffix);
        var wrapped = Text(WrappedVariable + suffix);
        if (raw is not null && wrapped is not null)
            return Fail($"Both {Variable + suffix} and {WrappedVariable + suffix} are set; set one.");
        if (raw is not null)
            return Key(Decode(raw), Variable + suffix, $"environment ({Variable + suffix})");
        return wrapped is null ? (null, null) : Unwrap(wrapped, suffix);
    }

    private (MasterKey?, string?) Unwrap(string wrappedText, string suffix)
    {
        var variable = WrappedVariable + suffix;
        var keyUrl = Text(VaultKeyVariable + suffix) ?? Text(VaultKeyVariable);
        if (keyUrl is null || !Uri.TryCreate(keyUrl, UriKind.Absolute, out var keyId) || keyId.Scheme != Uri.UriSchemeHttps)
            return Fail($"{variable} is set but {VaultKeyVariable} does not name a Key Vault key (https://<vault>/keys/<name>/<version>).");
        if (_vault is null) return Fail($"{variable} is set but this host cannot reach Azure Key Vault.");
        if (Decode(wrappedText) is not { } wrapped) return Fail($"{variable} is not base64.");
        try
        {
            return Key(_vault.Unwrap(keyId, wrapped), variable, $"Azure Key Vault key {keyId} ({variable})");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Fail($"Azure Key Vault key {keyId} did not unwrap {variable}: {ex.GetType().Name}: {FirstLine(ex.Message)}");
        }
    }

    private (MasterKey?, string?) Key(byte[]? key, string variable, string source)
    {
        if (key is { Length: KeyBytes }) return (new MasterKey(IdOf(key), key), source);
        if (key is not null) CryptographicOperations.ZeroMemory(key);
        return Fail($"{variable} is set but is not {(variable.Contains("WRAPPED", StringComparison.Ordinal) ? "a wrapped " : "the base64 of ")}"
            + $"{KeyBytes}-byte key; stored secret values cannot be read or written.");
    }

    private (MasterKey?, string?) Fail(string problem)
    {
        _problems.Add(problem);
        return (null, null);
    }

    private string? Text(string variable)
    {
        var text = _environment(variable) ?? FileText(_environment(variable + FileSuffix));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? FileText(string? path) => string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : File.ReadAllText(path);

    private static byte[]? Decode(string text)
    {
        var buffer = new byte[text.Length];
        try { return Convert.TryFromBase64String(text, buffer, out var written) ? buffer[..written] : null; }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static string FirstLine(string message) => message.Split('\n', 2)[0].Trim();

    private static string IdOf(byte[] key) => "env:" + Convert.ToHexStringLower(HMACSHA256.HashData(key, IdLabel))[..16];
}
