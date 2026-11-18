namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>2026-11-17-f3a8d: a value as the envelope leaves it — ciphertext with its tag, the wrapped data key, and the
/// nonces and key id that open them.</summary>
public sealed record SealedSecret(byte[] Ciphertext, byte[] Nonce, byte[] WrappedKey, byte[] WrapNonce, string KeyId);
