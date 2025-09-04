using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Engine.Pipelines.Css.Models;

namespace Engine.Pipelines.Css.Publish;

public class SourceMapGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<string> _sources = [];
    private readonly List<string?> _sourcesContent = [];
    private readonly List<CssMapping> _mappings = [];

    public void AddModule(CssModule module, int startLine, int startColumn)
    {
        ArgumentNullException.ThrowIfNull(module);
        int sourceIndex = _sources.Count;
        _sources.Add(GetRelativePath(module.FilePath));
        _sourcesContent.Add(module.Content);

        string[] lines = module.Content.Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            _mappings.Add(new CssMapping
            {
                GeneratedLine = startLine + lineIndex,
                GeneratedColumn = lineIndex == 0 ? startColumn : 0,
                SourceIndex = sourceIndex,
                OriginalLine = lineIndex,
                OriginalColumn = 0
            });
        }
    }

    public string GenerateSourceMap(string bundleFileName)
    {
        CssSourceMap map = new()
        {
            Version = 3,
            File = bundleFileName,
            Sources = [.. _sources],
            SourcesContent = [.. _sourcesContent],
            Mappings = EncodeMappings()
        };

        return JsonSerializer.Serialize(map, JsonOptions);
    }

    public string GenerateInlineSourceMap(string bundleFileName)
    {
        string sourceMap = GenerateSourceMap(bundleFileName);
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceMap));
        return $"/*# sourceMappingURL=data:application/json;charset=utf-8;base64,{base64} */";
    }

    private string EncodeMappings()
    {
        if (_mappings.Count == 0)
            return string.Empty;

        StringBuilder encoded = new();
        int previousGeneratedLine = 0;
        int previousGeneratedColumn = 0;
        int previousSourceIndex = 0;
        int previousOriginalLine = 0;
        int previousOriginalColumn = 0;

        foreach (CssMapping mapping in _mappings.OrderBy(m => m.GeneratedLine).ThenBy(m => m.GeneratedColumn))
        {
            if (mapping.GeneratedLine > previousGeneratedLine)
            {
                encoded.Append(new string(';', mapping.GeneratedLine - previousGeneratedLine));
                previousGeneratedColumn = 0;
            }
            else if (encoded.Length > 0 && encoded[^1] != ';')
            {
                encoded.Append(',');
            }

            encoded.Append(EncodeVlq(mapping.GeneratedColumn - previousGeneratedColumn));
            encoded.Append(EncodeVlq(mapping.SourceIndex - previousSourceIndex));
            encoded.Append(EncodeVlq(mapping.OriginalLine - previousOriginalLine));
            encoded.Append(EncodeVlq(mapping.OriginalColumn - previousOriginalColumn));

            previousGeneratedLine = mapping.GeneratedLine;
            previousGeneratedColumn = mapping.GeneratedColumn;
            previousSourceIndex = mapping.SourceIndex;
            previousOriginalLine = mapping.OriginalLine;
            previousOriginalColumn = mapping.OriginalColumn;
        }

        return encoded.ToString();
    }

    // Variable-length quantity (VLQ) encoding
    private static string EncodeVlq(int value)
    {
        const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        StringBuilder encoded = new();

        int vlq = value < 0 ? ((-value) << 1) | 1 : value << 1;

        do
        {
            int digit = vlq & 0x1F;
            vlq >>= 5;

            if (vlq > 0)
                digit |= 0x20;

            encoded.Append(Base64Chars[digit]);
        } while (vlq > 0);

        return encoded.ToString();
    }

    private static string GetRelativePath(string filePath)
    {
        string currentDir = Directory.GetCurrentDirectory();
        Uri fileUri = new( filePath);
        Uri currentUri = new(currentDir + Path.DirectorySeparatorChar);
        return currentUri.MakeRelativeUri(fileUri).ToString();
    }
}
