using System;

namespace Tester.Infrastructure;

public static class TestMode
{
    private const string EnvVar = "WEBSTIR_TEST_MODE";
    private static bool? _overrideFull;

    public static bool IsFull
    {
        get
        {
            if (_overrideFull.HasValue)
            {
                return _overrideFull.Value;
            }

            string? value = Environment.GetEnvironmentVariable(EnvVar);
            return string.Equals(value, "full", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetFull(bool full) => _overrideFull = full;
}
