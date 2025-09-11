namespace Engine.Pipelines.Testing;

public static class TestConstants
{
    public const string NodeExe = "node";
    public const string TesterResource = "Engine.Pipelines.Testing.tester.js";

    public static class JsonKeys
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Total = "total";
        public const string DurationMs = "durationMs";
        public const string Results = "results";
        public const string Name = "name";
        public const string File = "file";
        public const string Message = "message";
    }

    public static class Messages
    {
        public const string RunnerErrorTag = "[runner error]";
        public const string RunnerParseErrorTag = "[runner parse error]";
        public const string RunnerNoOutput = "No output from runner";
    }
}

