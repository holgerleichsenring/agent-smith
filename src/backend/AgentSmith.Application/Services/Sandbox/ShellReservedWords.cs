namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: the first tokens that are NOT a binary an image can carry — POSIX
/// shell keywords and builtins, plus the interpreters and wrappers that hide the real
/// command in an argument (<c>sh -c "…"</c>, <c>env</c>, <c>xargs</c>). A command
/// beginning with one of these is unprobed, never probed for the word itself.
/// </summary>
internal static class ShellReservedWords
{
    private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
    {
        // Keywords and control words.
        "if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done",
        "case", "esac", "in", "function", "select", "time", "coproc",
        // Builtins — no image carries them, and looking one up always "succeeds".
        "cd", "export", "set", "unset", "shift", "eval", "exec", "exit", "return",
        "source", ".", ":", "true", "false", "test", "[", "echo", "printf",
        "read", "local", "declare", "typeset", "readonly", "let", "alias", "unalias",
        "break", "continue", "trap", "wait", "umask", "ulimit", "times", "type",
        "hash", "pwd", "jobs", "fg", "bg", "kill", "getopts", "command", "builtin",
        // Interpreters and wrappers: the binary that matters sits in an argument.
        "sh", "bash", "dash", "zsh", "ksh", "csh", "tcsh", "fish", "env",
        "nohup", "nice", "timeout", "xargs", "watch",
    };

    public static bool Contains(string token) => Words.Contains(token);
}
