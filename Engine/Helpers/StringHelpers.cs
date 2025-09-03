using System.Globalization;
using System.Text.RegularExpressions;

namespace Engine.Helpers;

public static partial class StringHelpers
{
    public static int ExtractNumber(string str)
    {
        Match match = DigitsRegex().Match(str);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}
