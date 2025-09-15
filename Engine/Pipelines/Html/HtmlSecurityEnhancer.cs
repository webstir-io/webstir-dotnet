using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.Html;

public static class HtmlSecurityEnhancer
{
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    public static async Task<string> AddSRIForExternalResourcesAsync(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using HttpClient http = new()
        {
            Timeout = HttpTimeout
        };

        string result = await ProcessScriptsAsync(http, html).ConfigureAwait(false);
        result = await ProcessStylesAsync(http, result).ConfigureAwait(false);
        return result;
    }

    private static async Task<string> ProcessScriptsAsync(HttpClient http, string html)
    {
        return await RegexReplaceAsync(html, HtmlRegex.ExternalScriptTag(), async m =>
        {
            string tag = m.Value;
            string url = m.Groups["url"].Value;
            string? sri = await SubresourceIntegrity.ComputeForUrlAsync(http, url).ConfigureAwait(false);
            if (string.IsNullOrEmpty(sri))
            {
                return tag;
            }

            if (HtmlRegex.IntegrityAttr().IsMatch(tag))
            {
                return tag;
            }

            string injection = $" integrity=\"{sri}\" crossorigin=\"anonymous\"";
            int insertIndex = tag.IndexOf('>');
            if (insertIndex < 0)
            {
                return tag;
            }
            return tag.Insert(insertIndex, injection);
        });
    }

    private static async Task<string> ProcessStylesAsync(HttpClient http, string html)
    {
        return await RegexReplaceAsync(html, HtmlRegex.ExternalStylesheetLink(), async m =>
        {
            string tag = m.Value;
            string url = m.Groups["url"].Value;
            string? sri = await SubresourceIntegrity.ComputeForUrlAsync(http, url).ConfigureAwait(false);
            if (string.IsNullOrEmpty(sri))
            {
                return tag;
            }

            if (HtmlRegex.IntegrityAttr().IsMatch(tag))
            {
                return tag;
            }

            string injection = $" integrity=\"{sri}\" crossorigin=\"anonymous\"";
            int insertIndex = tag.IndexOf('>');
            if (insertIndex < 0)
            {
                return tag;
            }
            return tag.Insert(insertIndex, injection);
        });
    }

    private static async Task<string> RegexReplaceAsync(string input, Regex pattern, Func<Match, Task<string>> evaluator)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(evaluator);

        List<(int Index, int Length, string Replacement)> changes = [];
        foreach (Match match in pattern.Matches(input))
        {
            string replacement = await evaluator(match).ConfigureAwait(false);
            if (!string.Equals(replacement, match.Value, StringComparison.Ordinal))
            {
                changes.Add((match.Index, match.Length, replacement));
            }
        }

        if (changes.Count == 0)
        {
            return input;
        }

        changes.Sort((a, b) => b.Index.CompareTo(a.Index));
        System.Text.StringBuilder sb = new(input);
        foreach ((int Index, int Length, string Replacement) change in changes)
        {
            sb.Remove(change.Index, change.Length);
            sb.Insert(change.Index, change.Replacement);
        }
        return sb.ToString();
    }
}
