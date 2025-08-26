using Engine.Pipelines.Css.Models;
using System.Text.RegularExpressions;

namespace Engine.Pipelines.Css.Publish;

public static class Transformer
{
    // CSS Modules Processing
    public static CssProcessedModule ProcessModule(string content, string filePath)
    {
        if (!filePath.EndsWith(Css.ModuleExt, StringComparison.OrdinalIgnoreCase))
            return new CssProcessedModule { Content = content, ClassMappings = [] };

        string hash = CssModuleGraph.GenerateHash(filePath);
        HashSet<string> classNames = Parser.ExtractClassNames(content);
        Dictionary<string, string> mappings = [];

        foreach (string className in classNames)
        {
            string scopedName = $"{className}_{hash}";
            mappings[className] = scopedName;
            content = content.Replace($".{className}", $".{scopedName}");
        }

        return new CssProcessedModule
        {
            Content = content,
            ClassMappings = mappings
        };
    }

    // Autoprefixer
    private static readonly Dictionary<string, string[]> PrefixMap = new()
    {
        ["display: flex"] = ["-webkit-box", "-ms-flexbox"],
        ["display: inline-flex"] = ["-webkit-inline-box", "-ms-inline-flexbox"],
        ["flex"] = ["-webkit-flex", "-ms-flex"],
        ["flex-direction"] = ["-webkit-flex-direction", "-ms-flex-direction"],
        ["flex-wrap"] = ["-webkit-flex-wrap", "-ms-flex-wrap"],
        ["flex-flow"] = ["-webkit-flex-flow", "-ms-flex-flow"],
        ["justify-content"] = ["-webkit-justify-content", "-ms-flex-pack"],
        ["align-items"] = ["-webkit-align-items", "-ms-flex-align"],
        ["align-self"] = ["-webkit-align-self", "-ms-flex-item-align"],
        ["align-content"] = ["-webkit-align-content", "-ms-flex-line-pack"],
        ["order"] = ["-webkit-order", "-ms-flex-order"],
        ["flex-grow"] = ["-webkit-flex-grow", "-ms-flex-positive"],
        ["flex-shrink"] = ["-webkit-flex-shrink", "-ms-flex-negative"],
        ["flex-basis"] = ["-webkit-flex-basis", "-ms-flex-preferred-size"],
        ["transform"] = ["-webkit-transform", "-ms-transform"],
        ["transition"] = ["-webkit-transition"],
        ["animation"] = ["-webkit-animation"],
        ["user-select"] = ["-webkit-user-select", "-moz-user-select", "-ms-user-select"],
        ["box-shadow"] = ["-webkit-box-shadow"],
        ["border-radius"] = ["-webkit-border-radius"],
        ["background-clip"] = ["-webkit-background-clip"],
        ["background-size"] = ["-webkit-background-size"],
        ["appearance"] = ["-webkit-appearance", "-moz-appearance"]
    };

    public static string AddPrefixes(string css)
    {
        return CssRegex.RuleBlock().Replace(css, match =>
        {
            string block = match.Groups[1].Value;
            string prefixed = AddPrefixesToBlock(block);
            return $"{{{prefixed}}}";
        });
    }

    private static string AddPrefixesToBlock(string block)
    {
        Dictionary<string, string> properties = [];
        List<string> orderedProperties = [];

        MatchCollection matches = CssRegex.Property().Matches(block);
        foreach (Match match in matches)
        {
            string property = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();
            string key = $"{property}: {value}";

            if (PrefixMap.TryGetValue(property, out string[]? prefixes))
            {
                foreach (string prefix in prefixes)
                {
                    string prefixedProp = $"{prefix}: {value}";
                    if (!properties.ContainsKey(prefixedProp))
                    {
                        properties[prefixedProp] = prefixedProp;
                        orderedProperties.Add($"  {prefixedProp};");
                    }
                }
            }

            if (property == "display" && (value == "flex" || value == "inline-flex"))
            {
                string[] displayPrefixes = PrefixMap[$"display: {value}"];
                foreach (string prefix in displayPrefixes)
                {
                    string prefixedProp = $"display: {prefix}";
                    if (!properties.ContainsKey(prefixedProp))
                    {
                        properties[prefixedProp] = prefixedProp;
                        orderedProperties.Add($"  {prefixedProp};");
                    }
                }
            }

            string originalProp = $"{property}: {value}";
            if (!properties.ContainsKey(originalProp))
            {
                properties[originalProp] = originalProp;
                orderedProperties.Add($"  {originalProp};");
            }
        }

        return orderedProperties.Count > 0 
            ? "\n" + string.Join("\n", orderedProperties) + "\n"
            : block;
    }

    // Minifier
    public static string Minify(string css)
    {
        css = CssRegex.BlockComment().Replace(css, string.Empty);
        css = CssRegex.Whitespace().Replace(css, " ");
        css = CssRegex.OperatorSpace().Replace(css, "$1");
        css = CssRegex.ColonSpace().Replace(css, ":");
        css = CssRegex.LastSemicolon().Replace(css, "}");
        css = CssRegex.HexColor().Replace(css, "#$1$2$3");
        css = CssRegex.LeadingZero().Replace(css, "$1");
        css = CssRegex.TrailingZero().Replace(css, "$1");
        css = CssRegex.ZeroUnit().Replace(css, "0");
        css = css.Trim();

        return css;
    }
}