using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Engine.Processors.Css;

namespace Tests;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== WebStir Tests ===\n");
        
        // Run CSS Minification tests
        TestCssMinification();
    }
    
    static void TestCssMinification()
    {
        Console.WriteLine("Running CSS Minification Tests...\n");
        
        // We can now use the public CssMinifier directly!
        
        // Test 1: Basic minification
        TestBasicMinification();
        
        // Test 2: Edge cases
        TestEdgeCases();
        
        // Test 3: Real-world CSS
        TestRealWorldCss();
    }
    
    static void TestBasicMinification()
    {
        Console.WriteLine("Test 1: Basic Minification");
        
        var testCss = @"
/* Comment to remove */
.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px 15px 20px 15px;
    background-color: #ffffff;
    border: 1px solid #e0e0e0;
}

/* Another comment */
h1 {
    color: #333333;  /* Inline comment */
    font-size: 2.5rem;
    margin-bottom: 0px;
}";
        
        var minified = CssMinifier.Minify(testCss);
        var originalSize = testCss.Length;
        var minifiedSize = minified.Length;
        var reduction = (1 - (double)minifiedSize / originalSize) * 100;
        
        Console.WriteLine($"  Original: {originalSize} bytes");
        Console.WriteLine($"  Minified: {minifiedSize} bytes");
        Console.WriteLine($"  Reduction: {reduction:F1}%");
        Console.WriteLine($"  Result: {minified}");
        Console.WriteLine();
    }
    
    static void TestEdgeCases()
    {
        Console.WriteLine("Test 2: Edge Cases");
        
        // Test various edge cases
        var edgeCases = new Dictionary<string, string>
        {
            ["Empty CSS"] = "",
            ["Only comments"] = "/* comment 1 */ /* comment 2 */",
            ["URL with quotes"] = ".bg { background: url(\"image.jpg\"); }",
            ["Zero units"] = ".box { margin: 0px; padding: 0rem; border-width: 0em; }",
            ["Hex colors"] = ".colors { color: #336699; background: #ffffff; }",
            ["Media queries"] = "@media (max-width: 768px) { .box { padding: 10px; } }",
            ["Complex selectors"] = ".nav > ul > li > a:hover { color: red; }",
            ["Trailing semicolons"] = ".test { color: red; background: blue; }"
        };
        
        foreach (var testCase in edgeCases)
        {
            var minified = CssMinifier.Minify(testCase.Value);
            Console.WriteLine($"  {testCase.Key}:");
            Console.WriteLine($"    Input:  {testCase.Value}");
            Console.WriteLine($"    Output: {minified}");
        }
        Console.WriteLine();
    }
    
    static void TestRealWorldCss()
    {
        Console.WriteLine("Test 3: Real-world CSS");
        
        var realWorldCss = @"
/* Reset Styles */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

/* Typography */
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    font-size: 16px;
    line-height: 1.5;
    color: #333333;
    background-color: #ffffff;
}

/* Container */
.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 20px;
}

/* Buttons */
.btn {
    display: inline-block;
    padding: 10px 20px;
    background-color: #0066cc;
    color: #ffffff;
    text-decoration: none;
    border-radius: 4px;
    transition: background-color 0.3s ease;
}

.btn:hover {
    background-color: #0052a3;
}

/* Grid */
.grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 20px;
    margin: 20px 0;
}

/* Media Queries */
@media (max-width: 768px) {
    .container {
        padding: 0 10px;
    }
    
    .grid {
        grid-template-columns: 1fr;
        gap: 10px;
    }
}

/* Utilities */
.text-center { text-align: center; }
.mt-1 { margin-top: 0.5rem; }
.mt-2 { margin-top: 1rem; }
.mt-3 { margin-top: 1.5rem; }";
        
        var minified = CssMinifier.Minify(realWorldCss);
        var originalSize = realWorldCss.Length;
        var minifiedSize = minified.Length;
        var reduction = (1 - (double)minifiedSize / originalSize) * 100;
        
        Console.WriteLine($"  Original: {originalSize} bytes");
        Console.WriteLine($"  Minified: {minifiedSize} bytes");
        Console.WriteLine($"  Reduction: {reduction:F1}%");
        Console.WriteLine($"  Target: 30-50% reduction");
        Console.WriteLine($"  Status: {(reduction >= 30 ? "✓ PASSED" : "✗ FAILED")}");
        
        // Save output for inspection
        var outputPath = "test-output-minified.css";
        File.WriteAllText(outputPath, minified);
        Console.WriteLine($"  Output saved to: {outputPath}");
        
        // Show first 200 chars
        Console.WriteLine($"\n  Preview (first 200 chars):");
        Console.WriteLine($"  {minified.Substring(0, Math.Min(200, minified.Length))}...");
        Console.WriteLine();
    }
}