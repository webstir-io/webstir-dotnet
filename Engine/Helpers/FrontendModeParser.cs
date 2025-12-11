using System;

namespace Engine.Helpers;

public static class FrontendModeParser
{
    public const string FrontendMode = "--frontend-mode";

    public static string? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (int index = 0; index < args.Length; index += 1)
        {
            string current = args[index];
            if (!string.Equals(current, FrontendMode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"Missing value for {FrontendMode}. Expected bundle or ssg.");
            }

            return NormalizeMode(args[index + 1]);
        }

        return null;
    }

    private static string NormalizeMode(string value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" or "bundle" => "bundle",
            "ssg" => "ssg",
            _ => throw new ArgumentException(
                $"Unsupported frontend mode '{value}'. Expected bundle or ssg.")
        };
    }
}

