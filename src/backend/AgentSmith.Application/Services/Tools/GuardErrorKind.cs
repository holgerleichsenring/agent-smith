namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Discriminator for <see cref="GuardError"/>.
/// </summary>
public enum GuardErrorKind
{
    OutsideRepo,
    GitIgnored,
    InDotGit,
    WriteForbiddenInPhase,
    NotInBootstrapFiles
}
