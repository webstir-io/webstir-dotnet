using System;
using System.Collections.Generic;

namespace Tests.Framework;

public class TestResult
{
    public string TestName { get; set; } = "";
    public bool Passed
    {
        get; set;
    }
    public string Message { get; set; } = "";
    public TimeSpan Duration
    {
        get; set;
    }
    public Exception? Exception
    {
        get; set;
    }
}

public class TestSummary
{
    public int TotalTests
    {
        get; set;
    }
    public int PassedTests
    {
        get; set;
    }
    public int FailedTests
    {
        get; set;
    }
    public TimeSpan TotalDuration
    {
        get; set;
    }
    public List<TestResult> Results { get; set; } = [];
}
