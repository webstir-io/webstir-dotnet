using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Framework.Commands;

namespace Framework.Services;

internal interface IReleaseNotesService
{
    Task<ReleaseNotesResult> WriteAsync(string repositoryRoot, PackageBumpSummary bumpSummary, CancellationToken cancellationToken);
}

internal sealed record ReleaseNotesResult(IReadOnlyList<ReleaseNotesDocument> Documents)
{
    public static ReleaseNotesResult Empty { get; } = new(Array.Empty<ReleaseNotesDocument>());

    public bool HasDocuments => Documents.Count > 0;
}

internal sealed record ReleaseNotesDocument(string PackageName, string Version, string FilePath, IReadOnlyList<ReleaseNotesSection> Sections)
{
    public bool HasContent => Sections.Any(section => section.Items.Count > 0);
}

internal sealed record ReleaseNotesSection(string Title, IReadOnlyList<string> Items);

internal sealed class ReleaseNotesService : IReleaseNotesService
{
    private const string ReleaseNotesDirectoryName = "release-notes";
    private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();
    private static readonly string[] SectionOrder =
    [
        "Breaking Changes",
        "Features",
        "Fixes",
        "Performance",
        "Refactors",
        "Documentation",
        "Testing",
        "Chores",
        "Style",
        "Other"
    ];

    private static readonly Dictionary<string, string> TypeToSection = new(StringComparer.OrdinalIgnoreCase)
    {
        ["feat"] = "Features",
        ["feature"] = "Features",
        ["fix"] = "Fixes",
        ["bugfix"] = "Fixes",
        ["perf"] = "Performance",
        ["refactor"] = "Refactors",
        ["docs"] = "Documentation",
        ["doc"] = "Documentation",
        ["test"] = "Testing",
        ["tests"] = "Testing",
        ["ci"] = "Chores",
        ["build"] = "Chores",
        ["chore"] = "Chores",
        ["style"] = "Style"
    };

    public async Task<ReleaseNotesResult> WriteAsync(string repositoryRoot, PackageBumpSummary bumpSummary, CancellationToken cancellationToken)
    {
        if (!bumpSummary.HasPackages || bumpSummary.TargetVersion is null)
        {
            return ReleaseNotesResult.Empty;
        }

        string notesRoot = Path.Combine(repositoryRoot, "Framework", "Packaging", ReleaseNotesDirectoryName);
        Directory.CreateDirectory(notesRoot);

        List<ReleaseNotesDocument> documents = new(bumpSummary.Entries.Count);

        foreach (PackageBumpEntry entry in bumpSummary.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReleaseNotesDocument document = BuildDocument(notesRoot, bumpSummary.TargetVersion.Value.ToString(), entry);
            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            documents.Add(document);
        }

        return documents.Count == 0
            ? ReleaseNotesResult.Empty
            : new ReleaseNotesResult(documents);
    }

    private static ReleaseNotesDocument BuildDocument(string notesRoot, string version, PackageBumpEntry entry)
    {
        IReadOnlyDictionary<string, List<string>> sectionMap = InitializeSections();

        if (entry.CommitMessages.Count == 0)
        {
            sectionMap["Other"].Add("Version bump only (no notable commits detected).");
        }
        else
        {
            foreach (string rawMessage in entry.CommitMessages)
            {
                if (string.IsNullOrWhiteSpace(rawMessage))
                {
                    sectionMap["Other"].Add("Unlabeled change.");
                    continue;
                }

                ParsedCommit commit = ParseCommit(rawMessage);
                sectionMap[commit.Section].Add(commit.Message);
            }
        }

        List<ReleaseNotesSection> sections = new();
        foreach (string title in SectionOrder)
        {
            if (!sectionMap.TryGetValue(title, out List<string>? items) || items.Count == 0)
            {
                continue;
            }

            sections.Add(new ReleaseNotesSection(title, items));
        }

        if (sections.Count == 0)
        {
            sections.Add(new ReleaseNotesSection("Other", new[] { "Version bump only (no notable commits detected)." }));
        }

        string fileName = $"{SanitizeFileSegment(entry.PackageName)}-{version}.md";
        string filePath = Path.Combine(notesRoot, fileName);

        return new ReleaseNotesDocument(entry.PackageName, version, filePath, sections);
    }

    private static async Task WriteDocumentAsync(ReleaseNotesDocument document, CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        builder.Append("# ")
            .Append(document.PackageName)
            .Append(' ')
            .AppendLine(document.Version);
        builder.AppendLine();

        foreach (ReleaseNotesSection section in document.Sections)
        {
            builder.Append("## ")
                .AppendLine(section.Title);
            builder.AppendLine();

            foreach (string item in section.Items)
            {
                builder.Append("- ");
                builder.AppendLine(item);
            }

            builder.AppendLine();
        }

        await File.WriteAllTextAsync(document.FilePath, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static ParsedCommit ParseCommit(string rawMessage)
    {
        string trimmed = rawMessage.Trim();

        bool breaking = trimmed.Contains("BREAKING CHANGE", StringComparison.OrdinalIgnoreCase);

        int colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        string prefix = colonIndex >= 0 ? trimmed[..colonIndex] : trimmed;
        string remainder = colonIndex >= 0 ? trimmed[(colonIndex + 1)..].Trim() : trimmed;

        int scopeIndex = prefix.IndexOf('(', StringComparison.Ordinal);
        string type = scopeIndex >= 0 ? prefix[..scopeIndex] : prefix;
        type = type.Trim();

        if (!breaking && prefix.Contains('!', StringComparison.Ordinal))
        {
            breaking = true;
        }

        string section;
        if (breaking)
        {
            section = "Breaking Changes";
        }
        else if (!string.IsNullOrWhiteSpace(type) && TypeToSection.TryGetValue(type, out string? mapped))
        {
            section = mapped;
        }
        else
        {
            section = "Other";
        }

        string messageText = string.IsNullOrWhiteSpace(remainder) ? trimmed : remainder;

        return new ParsedCommit(section, messageText);
    }

    private static Dictionary<string, List<string>> InitializeSections()
    {
        Dictionary<string, List<string>> sections = new(StringComparer.OrdinalIgnoreCase);
        foreach (string title in SectionOrder)
        {
            sections[title] = new List<string>();
        }

        return sections;
    }

    private static string SanitizeFileSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "package";
        }

        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (character == '@')
            {
                continue;
            }

            if (character is '/' or '\\')
            {
                builder.Append('-');
                continue;
            }

            if (InvalidFileNameCharacters.Contains(character))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(character);
        }

        string sanitized = builder.ToString().Trim('-');
        return sanitized.Length == 0 ? "package" : sanitized;
    }

    private readonly record struct ParsedCommit(string Section, string Message);
}
