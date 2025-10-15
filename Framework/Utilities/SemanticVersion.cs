using System;
using System.Text.RegularExpressions;

namespace Framework.Utilities;

internal readonly partial record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = MyRegex();

    public static SemanticVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Match match = Pattern.Match(value.Trim());
        if (!match.Success)
        {
            throw new FormatException($"Invalid semantic version '{value}'.");
        }

        return new SemanticVersion(
            int.Parse(match.Groups["major"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    public static bool TryParse(string value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Match match = Pattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        version = new SemanticVersion(
            int.Parse(match.Groups["major"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }

    public SemanticVersion Increment(SemanticVersionBump bump)
    {
        return bump switch
        {
            SemanticVersionBump.Major => new SemanticVersion(Major + 1, 0, 0),
            SemanticVersionBump.Minor => new SemanticVersion(Major, Minor + 1, 0),
            _ => new SemanticVersion(Major, Minor, Patch + 1)
        };
    }

    public override string ToString() => FormattableString.Invariant($"{Major}.{Minor}.{Patch}");

    public int CompareTo(SemanticVersion other)
    {
        int majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        int minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        return Patch.CompareTo(other.Patch);
    }

    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}

internal enum SemanticVersionBump
{
    Patch = 0,
    Minor = 1,
    Major = 2
}
