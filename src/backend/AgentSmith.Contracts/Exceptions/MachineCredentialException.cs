namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// 2026-11-25-9992h: a machine identity's token could not be had from its hoster — a GitHub App whose installation token was
/// refused. A <see cref="MissingCredentialException"/>, so every act that refuses on a missing token refuses on this one too,
/// with the hoster's answer in the message.
/// </summary>
public sealed class MachineCredentialException(string entity, string secret, string answer)
    : MissingCredentialException(entity, secret,
        $"{entity} could not get a token for its machine identity (secret '{secret}'): {answer}");
