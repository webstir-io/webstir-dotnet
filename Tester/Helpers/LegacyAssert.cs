using System;
using Xunit;
using Xunit.Sdk;

namespace Tester.Helpers;

internal static class LegacyAssert
{
    public static void IsTrue(bool condition, string message = "") => Assert.True(condition, message);

    public static void IsFalse(bool condition, string message = "") => Assert.False(condition, message);

    public static void AreEqual<T>(T expected, T actual, string message = "") => Assert.Equal(expected, actual);

    public static void AreNotEqual<T>(T expected, T actual, string message = "") => Assert.NotEqual(expected, actual);

    public static void Contains(string expectedSubstring, string actualString, string message = "")
    {
        if (actualString is null || !actualString.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new XunitException($"Expected string to contain '{expectedSubstring}' but was '{actualString}'. {message}");
        }
    }

    public static void DoesNotContain(string expectedSubstring, string actualString, string message = "")
    {
        if (actualString is not null && actualString.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new XunitException($"Expected string not to contain '{expectedSubstring}' but was '{actualString}'. {message}");
        }
    }

    public static void IsNotNull(object? value, string message = "") => Assert.NotNull(value);

    public static void IsNull(object? value, string message = "") => Assert.Null(value);

    public static void LessThan<T>(T expected, T actual, string message = "") where T : IComparable<T>
    {
        if (actual.CompareTo(expected) >= 0)
        {
            throw new XunitException($"Expected less than '{expected}' but was '{actual}'. {message}");
        }
    }

    public static void GreaterThan<T>(T expected, T actual, string message = "") where T : IComparable<T>
    {
        if (actual.CompareTo(expected) <= 0)
        {
            throw new XunitException($"Expected greater than '{expected}' but was '{actual}'. {message}");
        }
    }

    public static void Fail(string message) => throw new XunitException(message);
}
