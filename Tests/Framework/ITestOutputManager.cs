using Microsoft.Extensions.Logging;

namespace Tests.Framework;

public interface ITestOutputManager
{
    Task WriteResultsAsync(TestSummary summary, string? outputPath = null, string? format = null);
    void WriteConsoleOutput(TestSummary summary);
}

public class TestOutputManager : ITestOutputManager
{
    private readonly ILogger<TestOutputManager> _logger;
    private readonly Dictionary<string, ITestResultFormatter> _formatters;
    
    public TestOutputManager(ILogger<TestOutputManager> logger)
    {
        _logger = logger;
        _formatters = new Dictionary<string, ITestResultFormatter>(StringComparer.OrdinalIgnoreCase)
        {
            ["console"] = new ConsoleFormatter(),
            ["json"] = new JsonFormatter(),
            ["xml"] = new XmlFormatter(),
            ["markdown"] = new MarkdownFormatter(),
            ["md"] = new MarkdownFormatter()
        };
    }
    
    public async Task WriteResultsAsync(TestSummary summary, string? outputPath = null, string? format = null)
    {
        // Always write to console
        WriteConsoleOutput(summary);
        
        // If output path specified, determine format and write file
        if (!string.IsNullOrEmpty(outputPath))
        {
            // Resolve full path relative to Tests directory
            string fullPath = ResolveOutputPath(outputPath, summary);
            
            // Auto-detect format from file extension if not specified
            if (string.IsNullOrEmpty(format))
            {
                format = Path.GetExtension(fullPath).TrimStart('.');
                if (string.IsNullOrEmpty(format))
                {
                    format = "json"; // Default to JSON if no extension
                }
            }
            
            if (_formatters.TryGetValue(format, out ITestResultFormatter? formatter))
            {
                string content = formatter.Format(summary);
                
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllTextAsync(fullPath, content);
                _logger.LogInformation("Test results written to {OutputPath} in {Format} format", fullPath, format);
                Console.WriteLine($"\nResults saved to: {fullPath}");
            }
            else
            {
                _logger.LogWarning("Unknown output format: {Format}. Available formats: {Formats}", 
                    format, string.Join(", ", _formatters.Keys));
                Console.WriteLine($"Warning: Unknown format '{format}'. Available: {string.Join(", ", _formatters.Keys)}");
            }
        }
    }
    
    private string ResolveOutputPath(string outputPath, TestSummary summary)
    {
        // If absolute path, use as-is
        if (Path.IsPathRooted(outputPath))
            return outputPath;
        
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        
        // If just a filename, organize by timestamp
        if (!outputPath.Contains('/') && !outputPath.Contains('\\'))
        {
            return Path.Combine("Tests", "results", $"{timestamp}-{outputPath}");
        }
        
        // Otherwise, make relative to Tests directory
        return Path.Combine("Tests", "results", outputPath);
    }
    
    public void WriteConsoleOutput(TestSummary summary)
    {
        ITestResultFormatter consoleFormatter = _formatters["console"];
        string output = consoleFormatter.Format(summary);
        Console.WriteLine(output);
    }
    
    // Method to get available formats for help/documentation
    public IEnumerable<string> GetAvailableFormats() => _formatters.Keys;
}
