using System.Text.RegularExpressions;

namespace Engine.Helpers;

public static class StringHelpers
{
    public static int ExtractNumber(string str)
    {
        Match match = Regex.Match(str, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }
}
