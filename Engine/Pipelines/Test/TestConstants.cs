namespace Engine.Pipelines.Test;

public static class TestConstants
{
    public const string NodeExe = "node";

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
        public const string RunnerMissingTag = "[runner missing]";
        public const string RunnerMissingInstructions = "@webstir/test CLI not found. Run 'npm install' to restore dependencies.";
    }
}
