using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Tests.Framework;

public interface ITestSuite
{
    string Name
    {
        get;
    }
    Task<TestResult[]> RunAsync();
}

public abstract class TestSuite : ITestSuite
{
    public abstract string Name
    {
        get;
    }
    public abstract Task<TestResult[]> RunAsync();

    protected TestResult RunTest(string testName, Action testAction)
    {
        ArgumentNullException.ThrowIfNull(testAction);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            testAction();
            stopwatch.Stop();
            return new TestResult
            {
                TestName = testName,
                Passed = true,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestResult
            {
                TestName = testName,
                Passed = false,
                Message = ex.Message,
                Exception = ex,
                Duration = stopwatch.Elapsed
            };
        }
    }

    protected async Task<TestResult> RunTestAsync(string testName, Func<Task> testAction)
    {
        ArgumentNullException.ThrowIfNull(testAction);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await testAction();
            stopwatch.Stop();
            return new TestResult
            {
                TestName = testName,
                Passed = true,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestResult
            {
                TestName = testName,
                Passed = false,
                Message = ex.Message,
                Exception = ex,
                Duration = stopwatch.Elapsed
            };
        }
    }

    protected async Task<TestResult[]> RunTestsAsync(
        IEnumerable<(string TestName, Func<Task> TestAction)> tests,
        bool runInParallel = false,
        int? maxDegreeOfParallelism = null)
    {
        ArgumentNullException.ThrowIfNull(tests);

        if (runInParallel)
        {
            int degree = maxDegreeOfParallelism.HasValue && maxDegreeOfParallelism.Value > 0
                ? maxDegreeOfParallelism.Value
                : Environment.ProcessorCount;
            SemaphoreSlim gate = new(degree);
            List<Task<TestResult>> runningTasks = new();

            foreach ((string TestName, Func<Task> TestAction) test in tests)
            {
                await gate.WaitAsync();
                Task<TestResult> task = Task.Run(async () =>
                {
                    try
                    {
                        return await RunTestAsync(test.TestName, test.TestAction);
                    }
                    finally
                    {
                        gate.Release();
                    }
                });
                runningTasks.Add(task);
            }

            TestResult[] parallelResults = await Task.WhenAll(runningTasks);
            gate.Dispose();
            return parallelResults;
        }

        List<TestResult> results = new();
        foreach ((string TestName, Func<Task> TestAction) test in tests)
        {
            results.Add(await RunTestAsync(test.TestName, test.TestAction));
        }

        return results.ToArray();
    }
}
