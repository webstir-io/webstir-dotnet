using System.Collections.Generic;

namespace Engine.Bridge.Test;

internal readonly record struct TestCliTestResult(
    string Name,
    string File,
    bool Passed,
    string? Message,
    long DurationMs);

internal readonly record struct TestCliRunResult(
    int Passed,
    int Failed,
    int Total,
    long DurationMs,
    IReadOnlyList<TestCliTestResult> Results,
    bool TestsDiscovered,
    bool HadErrors,
    int ExitCode);
