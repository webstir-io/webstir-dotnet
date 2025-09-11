using System.Collections.Generic;

namespace Engine.Pipelines.Testing;

public readonly record struct TestResult(
    string Name,
    string File,
    bool Passed,
    string? Message,
    long DurationMs);

public readonly record struct RunResult(
    int Passed,
    int Failed,
    int Total,
    long DurationMs,
    IReadOnlyList<TestResult> Results);

