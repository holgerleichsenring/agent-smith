namespace AgentSmith.Sandbox.Wire;

public static class SizeLimits
{
    public const long ReadFileMaxBytes = 1_048_576;
    public const long WriteFileMaxBytes = 10_485_760;
    public const int ListFilesMaxEntries = 1000;
    public const int GrepDefaultHeadLimit = 1000;

    // 2026-08-27-3eb1: directory_tree was the one read tool with NO cap at all — a
    // monorepo's tree is a single unbounded tool result, and it is the first call the
    // repository sweep makes. Capped like list_files.
    public const int DirectoryTreeMaxEntries = 1000;

    // 2026-08-27-3eb1: the ceiling on ONE tool result handed to an exploring model,
    // ~10k tokens at 4 chars/token. read_file alone may return 1 MB, which is a quarter
    // of a 128k window in a single reply.
    public const int ExploringToolResultMaxChars = 40_000;
    public const int RunCommandMaxBufferBytes = 1_048_576;
}
