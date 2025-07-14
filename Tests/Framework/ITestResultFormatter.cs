namespace Tests.Framework;

public interface ITestResultFormatter
{
    string Format(TestSummary summary);
    string FileExtension { get; }
}

public class ConsoleFormatter : ITestResultFormatter
{
    public string FileExtension => "";
    
    public string Format(TestSummary summary)
    {
        var result = new System.Text.StringBuilder();
        
        if (summary.FailedTests == 0)
        {
            result.AppendLine($"\n✅ All tests passed ({summary.TotalTests} tests, {summary.TotalDuration.TotalMilliseconds:F0}ms)");
        }
        else
        {
            result.AppendLine($"\n❌ {summary.FailedTests} of {summary.TotalTests} tests failed ({summary.TotalDuration.TotalMilliseconds:F0}ms)");
            result.AppendLine();
            foreach (var failedTest in summary.Results.Where(r => !r.Passed))
            {
                result.AppendLine($"✗ {failedTest.TestName}: {failedTest.Message}");
            }
        }
        
        return result.ToString();
    }
}

public class JsonFormatter : ITestResultFormatter
{
    public string FileExtension => "json";
    
    public string Format(TestSummary summary)
    {
        var result = new
        {
            t = summary.TotalTests,
            p = summary.PassedTests,
            f = summary.FailedTests,
            d = (int)summary.TotalDuration.TotalMilliseconds,
            ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            r = summary.Results.Select(r => new
            {
                n = r.TestName,
                ok = r.Passed,
                m = r.Passed ? null : r.Message,
                d = (int)r.Duration.TotalMilliseconds
            }).Where(r => r.m != null || r.d > 1) // Only include failed tests or slow tests
        };
        
        return System.Text.Json.JsonSerializer.Serialize(result);
    }
}

public class XmlFormatter : ITestResultFormatter
{
    public string FileExtension => "xml";
    
    public string Format(TestSummary summary)
    {
        var xml = new System.Text.StringBuilder();
        xml.AppendLine($"<ts t=\"{summary.TotalTests}\" f=\"{summary.FailedTests}\" d=\"{summary.TotalDuration.TotalMilliseconds:F0}\">");
        
        // Only include failed tests and slow tests (>1ms) to reduce size
        foreach (var result in summary.Results.Where(r => !r.Passed || r.Duration.TotalMilliseconds > 1))
        {
            if (!result.Passed)
            {
                xml.AppendLine($"  <tc n=\"{EscapeXml(result.TestName)}\" f=\"{EscapeXml(result.Message)}\"/>");
            }
            else
            {
                xml.AppendLine($"  <tc n=\"{EscapeXml(result.TestName)}\" d=\"{result.Duration.TotalMilliseconds:F0}\"/>");
            }
        }
        
        xml.AppendLine("</ts>");
        return xml.ToString();
    }
    
    private static string EscapeXml(string text) => 
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

public class MarkdownFormatter : ITestResultFormatter
{
    public string FileExtension => "md";
    
    public string Format(TestSummary summary)
    {
        var md = new System.Text.StringBuilder();
        
        md.AppendLine($"# Tests: {summary.PassedTests}/{summary.TotalTests} ({summary.TotalDuration.TotalMilliseconds:F0}ms)");
        
        // Only show failed tests and slow tests to minimize size
        var notableResults = summary.Results.Where(r => !r.Passed || r.Duration.TotalMilliseconds > 1).ToList();
        
        if (notableResults.Any())
        {
            md.AppendLine();
            foreach (var result in notableResults)
            {
                var status = result.Passed ? "🐌" : "❌";
                var detail = result.Passed ? $"{result.Duration.TotalMilliseconds:F0}ms" : result.Message;
                md.AppendLine($"- {status} {result.TestName}: {detail}");
            }
        }
        
        return md.ToString();
    }
}