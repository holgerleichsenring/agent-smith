namespace AgentSmith.Sandbox.Wire;

public static class SizeLimits
{
    public const long ReadFileMaxBytes = 1_048_576;
    public const long WriteFileMaxBytes = 10_485_760;

    // 2026-10-08-e8b9j: WriteBytes carries at most 4 MB decoded per step (a step is one Redis
    // message, and Redis runs at 256 MB with LRU eviction), and a file assembled from chunks is
    // at most 25 MB — the per-file bound an upload is held to (ReferenceSetLimits.MaxFileBytes).
    public const long WriteBytesChunkMaxBytes = 4L * 1024 * 1024;
    public const long WriteBytesMaxBytes = 25L * 1024 * 1024;
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

    // 2026-10-07-6b9db: what run_command hands the model, per section. stdout is the content,
    // stderr an excerpt (its first error and its summary); together they stay under the tool
    // loop's 100,000 bound, so the loop never cuts across both sections at once. The error
    // line is bounded too: the agent's OutputTail keeps a last line whole, however long.
    public const int RunCommandStdoutMaxChars = 60_000;
    public const int RunCommandStderrMaxChars = 20_000;
    public const int RunCommandErrorLineMaxChars = 8_000;

    // The sandbox agent stores at most this many stdout characters in a run step's result
    // body, with no flag (StepExecutor.MaxCapturedOutputChars). A body this long may be cut.
    public const int RunStepCapturedStdoutMaxChars = 1_000_000;
}
