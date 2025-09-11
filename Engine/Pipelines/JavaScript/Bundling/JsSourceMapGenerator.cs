using System.Text;
using System.Text.Json;

using Engine.Pipelines.JavaScript.Models;

using System.Collections.Generic;
using System.Linq;
using System;

namespace Engine.Pipelines.JavaScript.Bundling;

public class JsSourceMapGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };
    private readonly List<JsMappingSegment> _mappings = [];
    private readonly List<string> _sources = [];
    private readonly List<string> _names = [];
    private int _currentLine;
    private int _currentColumn = 0;

    public void Clear()
    {
        _mappings.Clear();
        _sources.Clear();
        _names.Clear();
        _currentLine = 0;
        _currentColumn = 0;
    }

    public void AddMapping(JsModuleInfo module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!_sources.Contains(module.FilePath))
            _sources.Add(module.FilePath);

        int sourceIndex = _sources.IndexOf(module.FilePath);

        _mappings.Add(new JsMappingSegment
        {
            GeneratedLine = _currentLine,
            GeneratedColumn = _currentColumn,
            SourceIndex = sourceIndex,
            OriginalLine = 0,
            OriginalColumn = 0
        });

        int lineCount = module.Content.Count(c => c == '\n');
        _currentLine += lineCount + 2;
    }

    public void AddMapping(int generatedLine, int generatedColumn, string source, int originalLine, int originalColumn, string? name = null)
    {
        if (!_sources.Contains(source))
            _sources.Add(source);

        int sourceIndex = _sources.IndexOf(source);
        int? nameIndex = null;

        if (name != null)
        {
            if (!_names.Contains(name))
                _names.Add(name);
            nameIndex = _names.IndexOf(name);
        }

        _mappings.Add(new JsMappingSegment
        {
            GeneratedLine = generatedLine,
            GeneratedColumn = generatedColumn,
            SourceIndex = sourceIndex,
            OriginalLine = originalLine,
            OriginalColumn = originalColumn,
            NameIndex = nameIndex
        });
    }

    public string Generate()
    {
        JsSourceMap map = new()
        {
            Version = 3,
            Sources = [.. _sources],
            Names = [.. _names],
            Mappings = EncodeMappings()
        };

        return JsonSerializer.Serialize(map, JsonOptions);
    }

    private string EncodeMappings()
    {
        if (_mappings.Count == 0)
            return string.Empty;

        List<JsMappingSegment> sorted = [.. _mappings.OrderBy(m => m.GeneratedLine).ThenBy(m => m.GeneratedColumn)];
        StringBuilder result = new();

        int previousGeneratedLine = 0;
        int previousGeneratedColumn = 0;
        int previousSourceIndex = 0;
        int previousOriginalLine = 0;
        int previousOriginalColumn = 0;
        int previousNameIndex = 0;

        foreach (JsMappingSegment segment in sorted)
        {
            while (previousGeneratedLine < segment.GeneratedLine)
            {
                result.Append(';');
                previousGeneratedLine++;
                previousGeneratedColumn = 0;
            }

            if (previousGeneratedColumn > 0)
                result.Append(',');

            result.Append(EncodeVLQ(segment.GeneratedColumn - previousGeneratedColumn));
            result.Append(EncodeVLQ(segment.SourceIndex - previousSourceIndex));
            result.Append(EncodeVLQ(segment.OriginalLine - previousOriginalLine));
            result.Append(EncodeVLQ(segment.OriginalColumn - previousOriginalColumn));

            if (segment.NameIndex.HasValue)
            {
                result.Append(EncodeVLQ(segment.NameIndex.Value - previousNameIndex));
                previousNameIndex = segment.NameIndex.Value;
            }

            previousGeneratedColumn = segment.GeneratedColumn;
            previousSourceIndex = segment.SourceIndex;
            previousOriginalLine = segment.OriginalLine;
            previousOriginalColumn = segment.OriginalColumn;
        }

        return result.ToString();
    }

    private string EncodeVLQ(int value)
    {
        const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        StringBuilder result = new();

        int vlq = value < 0 ? ((-value) << 1) + 1 : value << 1;

        do
        {
            int digit = vlq & 0x1F;
            vlq >>= 5;

            if (vlq > 0)
                digit |= 0x20;

            result.Append(Base64Chars[digit]);
        } while (vlq > 0);

        return result.ToString();
    }
}
