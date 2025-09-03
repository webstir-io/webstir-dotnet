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
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
}
