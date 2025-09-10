using System;
using System.Collections.Generic;

using Engine.Pipelines.Css.Models;
using Engine.Pipelines.Css.Tokenization;

namespace Engine.Pipelines.Css.Publish;

public static class Transformer
{
    // CSS Modules Processing
    public static CssProcessedModule ProcessModule(string content, string filePath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(filePath);
        if (!filePath.EndsWith(Css.ModuleExt, StringComparison.OrdinalIgnoreCase))
        {
            return new CssProcessedModule { Content = content, ClassMappings = [] };
        }

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

    // Token-aware minimal prefixing for modern browsers
    public static string AddPrefixes(string css)
    {
        ArgumentNullException.ThrowIfNull(css);

        CssTokenizer tokenizer = new(css);
        List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);
        List<CssToken> output = [];

        // Track per-block seen flags to avoid duplicates
        Stack<(bool webkitUserSelect, bool webkitAppearance)> seenStack = new();
        seenStack.Push((false, false));

        int tokenIndex = 0;
        while (tokenIndex < tokens.Count)
        {
            CssToken token = tokens[tokenIndex];
            if (token.Type == CssTokenType.Eof)
            {
                output.Add(token);
                break;
            }

            if (token.Type == CssTokenType.LBrace)
            {
                // Enter new block
                output.Add(token);
                seenStack.Push((false, false));
                tokenIndex++;
                continue;
            }
            if (token.Type == CssTokenType.RBrace)
            {
                // Exit block
                if (seenStack.Count > 1)
                {
                    seenStack.Pop();
                }
                output.Add(token);
                tokenIndex++;
                continue;
            }

            // Track existing prefixed declarations in the current block
            if (token.Type == CssTokenType.Ident && seenStack.Count > 0)
            {
                string ident = token.Value;
                (bool webkitUserSelect, bool webkitAppearance) flags = seenStack.Peek();
                if (ident.Equals("-webkit-user-select", StringComparison.OrdinalIgnoreCase))
                {
                    seenStack.Pop();
                    seenStack.Push((true, flags.webkitAppearance));
                }
                else if (ident.Equals("-webkit-appearance", StringComparison.OrdinalIgnoreCase))
                {
                    seenStack.Pop();
                    seenStack.Push((flags.webkitUserSelect, true));
                }

                // Detect user-select / appearance property and inject prefixed version
                if (ident.Equals("user-select", StringComparison.OrdinalIgnoreCase)
                    || ident.Equals("appearance", StringComparison.OrdinalIgnoreCase))
                {
                    // Lookahead for ':' and capture value tokens until ';' or '}'
                    int afterNameIndex = tokenIndex + 1;
                    while (afterNameIndex < tokens.Count && (tokens[afterNameIndex].Type == CssTokenType.Whitespace || tokens[afterNameIndex].Type == CssTokenType.Comment))
                    {
                        afterNameIndex++;
                    }
                    if (afterNameIndex < tokens.Count && tokens[afterNameIndex].Type == CssTokenType.Colon)
                    {
                        int valueStartIndex = afterNameIndex + 1;
                        List<CssToken> valueTokens = [];
                        bool endedWithSemicolon = false;
                        while (valueStartIndex < tokens.Count)
                        {
                            CssToken valueToken = tokens[valueStartIndex];
                            if (valueToken.Type == CssTokenType.Semicolon)
                            {
                                endedWithSemicolon = true;
                                break;
                            }
                            if (valueToken.Type == CssTokenType.RBrace)
                            {
                                break;
                            }
                            valueTokens.Add(valueToken);
                            valueStartIndex++;
                        }

                        // Insert prefixed declaration if not already present
                        (bool webkitUserSelect, bool webkitAppearance) cur = seenStack.Peek();
                        bool needWebkit = ident.Equals("user-select", StringComparison.OrdinalIgnoreCase) && !cur.webkitUserSelect
                            || ident.Equals("appearance", StringComparison.OrdinalIgnoreCase) && !cur.webkitAppearance;
                        if (needWebkit)
                        {
                            string prefixedName = ident.Equals("user-select", StringComparison.OrdinalIgnoreCase)
                                ? "-webkit-user-select"
                                : "-webkit-appearance";

                            output.Add(new CssToken(CssTokenType.Ident, prefixedName, 0, 0));
                            output.Add(new CssToken(CssTokenType.Colon, ":", 0, 0));
                            foreach (CssToken vt in valueTokens)
                            {
                                output.Add(vt);
                            }
                            // Always terminate with semicolon; serializer will drop it if trailing
                            output.Add(new CssToken(CssTokenType.Semicolon, ";", 0, 0));

                            // Update seen flags
                            if (ident.Equals("user-select", StringComparison.OrdinalIgnoreCase))
                            {
                                seenStack.Pop();
                                seenStack.Push((true, cur.webkitAppearance));
                            }
                            else
                            {
                                seenStack.Pop();
                                seenStack.Push((cur.webkitUserSelect, true));
                            }
                        }

                        // Copy original declaration tokens as-is
                        while (tokenIndex <= valueStartIndex && tokenIndex < tokens.Count)
                        {
                            output.Add(tokens[tokenIndex]);
                            tokenIndex++;
                        }
                        // If ended by RBrace, do not consume it here; loop will process it
                        if (!endedWithSemicolon && tokenIndex < tokens.Count && tokens[tokenIndex].Type == CssTokenType.RBrace)
                        {
                            // fall through; RBrace handled next iteration
                        }
                        continue;
                    }
                }
            }

            // Default: copy token
            output.Add(token);
            tokenIndex++;
        }

        // Serialize back
        string result = CssSerializer.Serialize(output);
        return result;
    }

    // Strip legacy prefixes and obsolete values for modern targets
    public static string StripLegacyPrefixes(string css)
    {
        ArgumentNullException.ThrowIfNull(css);

        CssTokenizer tokenizer = new(css);
        List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);
        List<CssToken> output = [];

        int index = 0;
        while (index < tokens.Count)
        {
            CssToken token = tokens[index];
            if (token.Type == CssTokenType.Eof)
            {
                output.Add(token);
                break;
            }

            // Detect a declaration: IDENT ':'
            if (token.Type == CssTokenType.Ident)
            {
                string propName = token.Value;
                int afterName = index + 1;
                while (afterName < tokens.Count && (tokens[afterName].Type == CssTokenType.Whitespace || tokens[afterName].Type == CssTokenType.Comment))
                {
                    afterName++;
                }
                if (afterName < tokens.Count && tokens[afterName].Type == CssTokenType.Colon)
                {
                    // Capture declaration tokens (name through end before ';' or '}')
                    int valueStart = afterName + 1;
                    int cursor = valueStart;
                    List<CssToken> declTokens = [];
                    // Include name and tokens up to before terminator
                    for (int i = index; i < valueStart; i++)
                    {
                        declTokens.Add(tokens[i]);
                    }
                    bool endedWithSemicolon = false;
                    while (cursor < tokens.Count)
                    {
                        CssToken vt = tokens[cursor];
                        if (vt.Type == CssTokenType.Semicolon)
                        {
                            endedWithSemicolon = true;
                            break;
                        }
                        if (vt.Type == CssTokenType.RBrace)
                        {
                            break;
                        }
                        declTokens.Add(vt);
                        cursor++;
                    }

                    // Decide drop/keep
                    if (ShouldDropDeclaration(propName, declTokens))
                    {
                        // Skip declaration and optional semicolon
                        index = endedWithSemicolon ? cursor + 1 : cursor;
                        continue;
                    }
                    else
                    {
                        // Keep declaration as-is
                        foreach (CssToken t in declTokens)
                        {
                            output.Add(t);
                        }
                        // Include semicolon if present
                        if (endedWithSemicolon)
                        {
                            output.Add(tokens[cursor]);
                            index = cursor + 1;
                        }
                        else
                        {
                            index = cursor;
                        }
                        continue;
                    }
                }
            }

            // Default: copy token
            output.Add(token);
            index++;
        }

        return CssSerializer.Serialize(output);
    }

    private static bool ShouldDropDeclaration(string propName, List<CssToken> declTokens)
    {
        // Normalize for comparisons
        string name = propName.ToLowerInvariant();

        // Drop vendor properties by prefix
        if (name.StartsWith("-ms-", StringComparison.Ordinal)
            || name.StartsWith("-o-", StringComparison.Ordinal)
            || name.StartsWith("-khtml-", StringComparison.Ordinal))
        {
            return true;
        }

        // Drop -webkit- properties unless explicitly allowed for modern Safari behavior
        if (name.StartsWith("-webkit-", StringComparison.Ordinal))
        {
            if (!IsAllowedWebkitProperty(name))
            {
                return true;
            }
        }

        // Drop legacy flexbox display values
        if (name.Equals("display", StringComparison.Ordinal))
        {
            for (int i = 0; i < declTokens.Count; i++)
            {
                if (declTokens[i].Type == CssTokenType.Ident)
                {
                    string val = declTokens[i].Value;
                    if (val.Equals("-webkit-box", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("-webkit-inline-box", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("-ms-flexbox", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsAllowedWebkitProperty(string lowerName)
    {
        // Keep a small allowlist of still-useful -webkit- properties
        if (lowerName.Equals("-webkit-user-select", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-appearance", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-text-size-adjust", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-tap-highlight-color", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-overflow-scrolling", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-line-clamp", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-backdrop-filter", StringComparison.Ordinal)
            || lowerName.Equals("-webkit-background-clip", StringComparison.Ordinal))
        {
            return true;
        }

        if (lowerName.StartsWith("-webkit-mask", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    // Minifier (token-based)
    public static string Minify(string css)
    {
        ArgumentNullException.ThrowIfNull(css);

        // Tokenize with license comment preservation
        CssTokenizer tokenizer = new(css);
        List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);

        // Token-level minification passes
        List<CssToken> minified = CssTokenMinifier.Minify(tokens);

        // Serialize with safe spacing and trailing semicolon removal
        string result = CssSerializer.Serialize(minified);
        return result.Trim();
    }

    // (legacy helper removed; tokenization now handles sensitivity)
}
