using System;

namespace Engine.Workflows;

internal static class RuntimeOptionParser
{
    public static string? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (int index = 0; index < args.Length; index += 1)
        {
            string current = args[index];
            if (!IsRuntimeFlag(current))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"Missing value for {TestOptions.Runtime}. Expected frontend, backend, or all.");
            }

            return NormalizeRuntime(args[index + 1]);
        }

        return null;
    }

    private static bool IsRuntimeFlag(string? value) =>
        string.Equals(value, TestOptions.Runtime, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, TestOptions.RuntimeShort, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRuntime(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" or "all" => null,
            "frontend" or "backend" => normalized,
            _ => throw new ArgumentException(
                $"Unsupported runtime '{value}'. Expected frontend, backend, or all.")
        };
    }
}
