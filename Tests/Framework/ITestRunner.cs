using Microsoft.Extensions.Logging;

namespace Tests.Framework;

public interface ITestRunner
{
    Task<TestSummary> RunAllTestsAsync();
    Task<TestSummary> RunTestsAsync(IEnumerable<string> testSuiteNames);
}

public class TestRunner(IEnumerable<ITestSuite> testSuites, ILogger<TestRunner> logger) : ITestRunner
{
    private readonly IEnumerable<ITestSuite> _testSuites = testSuites;
    private readonly ILogger<TestRunner> _logger = logger;
    
    public Task<TestSummary> RunAllTestsAsync() => RunTestsInternalAsync(_testSuites);
    
    public async Task<TestSummary> RunTestsAsync(IEnumerable<string> testSuiteNames)
    {
        IEnumerable<ITestSuite> filteredSuites = _testSuites.Where(suite => 
            testSuiteNames.Any(name => suite.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        
        if (!filteredSuites.Any())
        {
            _logger.LogWarning("No test suites found matching: {TestSuiteNames}", string.Join(", ", testSuiteNames));
        }
        
        return await RunTestsInternalAsync(filteredSuites);
    }
    
    private async Task<TestSummary> RunTestsInternalAsync(IEnumerable<ITestSuite> suitesToRun)
    {
        List<TestResult> allResults = [];
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (ITestSuite suite in suitesToRun)
        {
            _logger.LogInformation("Running {SuiteName}", suite.Name);
            
            TestResult[] results = await suite.RunAsync();
            allResults.AddRange(results);
            
            // Log failures immediately
            foreach (TestResult result in results.Where(r => !r.Passed))
            {
                _logger.LogError("Test failed: {TestName} - {Message}", result.TestName, result.Message);
            }
        }
        
        stopwatch.Stop();
        
        return new TestSummary
        {
            TotalTests = allResults.Count,
            PassedTests = allResults.Count(r => r.Passed),
            FailedTests = allResults.Count(r => !r.Passed),
            TotalDuration = stopwatch.Elapsed,
            Results = allResults
        };
    }
}
