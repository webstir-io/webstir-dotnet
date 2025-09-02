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
        ArgumentNullException.ThrowIfNull(summary);
        System.Text.StringBuilder result = new();
        
        if (summary.FailedTests == 0)
        {
            result.AppendLine(FormattableString.Invariant($"\n✅ All tests passed ({summary.TotalTests} tests, {summary.TotalDuration.TotalMilliseconds:F0}ms)"));
        }
        else
        {
            result.AppendLine(FormattableString.Invariant($"\n❌ {summary.FailedTests} of {summary.TotalTests} tests failed ({summary.TotalDuration.TotalMilliseconds:F0}ms)"));
            result.AppendLine();
            foreach (TestResult failedTest in summary.Results.Where(r => !r.Passed))
            {
                result.AppendLine(FormattableString.Invariant($"✗ {failedTest.TestName}: {failedTest.Message}"));
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
        ArgumentNullException.ThrowIfNull(summary);
        var result = new
        {
            t = summary.TotalTests,
            p = summary.PassedTests,
            f = summary.FailedTests,
            d = (int)summary.TotalDuration.TotalMilliseconds,
            ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
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
        ArgumentNullException.ThrowIfNull(summary);
        System.Text.StringBuilder xml = new();
        xml.AppendLine(FormattableString.Invariant($"<ts t=\"{summary.TotalTests}\" f=\"{summary.FailedTests}\" d=\"{summary.TotalDuration.TotalMilliseconds:F0}\">"));
        
        // Only include failed tests and slow tests (>1ms) to reduce size
        foreach (TestResult result in summary.Results.Where(r => !r.Passed || r.Duration.TotalMilliseconds > 1))
        {
            if (!result.Passed)
            {
                xml.AppendLine(FormattableString.Invariant($"  <tc n=\"{EscapeXml(result.TestName)}\" f=\"{EscapeXml(result.Message)}\"/>"));
            }
            else
            {
                xml.AppendLine(FormattableString.Invariant($"  <tc n=\"{EscapeXml(result.TestName)}\" d=\"{result.Duration.TotalMilliseconds:F0}\"/>"));
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
        ArgumentNullException.ThrowIfNull(summary);
        System.Text.StringBuilder md = new();
        
        md.AppendLine(FormattableString.Invariant($"# Tests: {summary.PassedTests}/{summary.TotalTests} ({summary.TotalDuration.TotalMilliseconds:F0}ms)"));
        
        // Only show failed tests and slow tests to minimize size
        List<TestResult> notableResults = [.. summary.Results.Where(r => !r.Passed || r.Duration.TotalMilliseconds > 1)];
        
        if (notableResults.Any())
        {
            md.AppendLine();
            foreach (TestResult result in notableResults)
            {
                string status = result.Passed ? "🐌" : "❌";
                string detail = result.Passed ? FormattableString.Invariant($"{result.Duration.TotalMilliseconds:F0}ms") : result.Message;
                md.AppendLine(FormattableString.Invariant($"- {status} {result.TestName}: {detail}"));
            }
        }
        
        return md.ToString();
    }
}
