using System.Text.RegularExpressions;

namespace Engine.Processors;

/// <summary>
/// Handles CSS minification by removing comments, collapsing whitespace, and optimizing syntax
/// </summary>
public static class CssMinifier
{
    /// <summary>
    /// Minifies CSS by removing comments, collapsing whitespace, and optimizing syntax
    /// </summary>
    public static string Minify(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return string.Empty;

        // First remove comments
        css = RemoveComments(css);
        
        // Collapse multiple whitespace characters into single space
        css = Regex.Replace(css, @"\s+", " ");
        
        // Remove unnecessary spaces around CSS syntax characters
        css = Regex.Replace(css, @"\s*([{}:;,>+~])\s*", "$1");
        
        // Remove trailing semicolon before closing brace
        css = Regex.Replace(css, @";\s*}", "}");
        
        // Remove quotes from url() when possible
        css = Regex.Replace(css, @"url\s*\(\s*['""]([^'""]+)['""]?\s*\)", "url($1)");
        
        // Remove unnecessary 0 units
        css = Regex.Replace(css, @":\s*0(px|em|rem|%|pt|pc|mm|cm|in|ex|ch|vw|vh|vmin|vmax)", ":0");
        
        // Shorten hex colors where possible
        css = Regex.Replace(css, @"#([0-9a-fA-F])\1([0-9a-fA-F])\2([0-9a-fA-F])\3\b", "#$1$2$3");
        
        // Remove the last semicolon in a rule
        css = Regex.Replace(css, @";\s*}", "}");
        
        // Ensure there's no leading/trailing whitespace
        return css.Trim();
    }
    
    /// <summary>
    /// Removes CSS comments from the provided CSS string
    /// </summary>
    public static string RemoveComments(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return string.Empty;

        // Remove CSS comments (/* ... */)
        var commentPattern = @"/\*[\s\S]*?\*/";
        css = Regex.Replace(css, commentPattern, string.Empty);
        
        // Remove empty lines left by comment removal
        var emptyLinePattern = @"^\s*\r?\n";
        css = Regex.Replace(
            css, 
            emptyLinePattern, 
            string.Empty, 
            RegexOptions.Multiline
        );
        
        // Trim whitespace from beginning and end
        return css.Trim();
    }
}