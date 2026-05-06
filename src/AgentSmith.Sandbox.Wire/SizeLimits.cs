namespace AgentSmith.Sandbox.Wire;

public static class SizeLimits
{
    public const long ReadFileMaxBytes = 1_048_576;
    public const long WriteFileMaxBytes = 10_485_760;
    public const int ListFilesMaxEntries = 1000;
    public const int GrepMaxMatches = 200;
}
