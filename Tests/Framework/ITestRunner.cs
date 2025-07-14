using Microsoft.Extensions.Logging;

namespace Tests.Framework;

public interface ITestRunner
{
    Task<TestSummary> RunAllTestsAsync();
    Task<TestSummary> RunTestsAsync(IEnumerable<string> testSuiteNames);
}

public class TestRunner : ITestRunner
{
    private readonly IEnumerable<ITestSuite> _testSuites;
    private readonly ILogger<TestRunner> _logger;
    
    public TestRunner(IEnumerable<ITestSuite> testSuites, ILogger<TestRunner> logger)
    {
        _testSuites = testSuites;
        _logger = logger;
    }
    
    public async Task<TestSummary> RunAllTestsAsync()
    {
        return await RunTestsInternalAsync(_testSuites);
    }
    
    public async Task<TestSummary> RunTestsAsync(IEnumerable<string> testSuiteNames)
    {
        var filteredSuites = _testSuites.Where(suite => 
            testSuiteNames.Any(name => suite.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        
        if (!filteredSuites.Any())
        {
            _logger.LogWarning("No test suites found matching: {TestSuiteNames}", string.Join(", ", testSuiteNames));
        }
        
        return await RunTestsInternalAsync(filteredSuites);
    }
    
    private async Task<TestSummary> RunTestsInternalAsync(IEnumerable<ITestSuite> suitesToRun)
    {
        var allResults = new List<TestResult>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var suite in suitesToRun)
        {
            _logger.LogInformation("Running {SuiteName}", suite.Name);
            
            var results = await suite.RunAsync();
            allResults.AddRange(results);
            
            // Log failures immediately
            foreach (var result in results.Where(r => !r.Passed))
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