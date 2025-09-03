namespace Tests.Framework;

public static class Assert
{
    public static void IsTrue(bool condition, string message = "")
    {
        if (!condition)
            throw new AssertionException($"Expected true but was false. {message}");
    }

    public static void IsFalse(bool condition, string message = "")
    {
        if (condition)
            throw new AssertionException($"Expected false but was true. {message}");
    }

    public static void AreEqual<T>(T expected, T actual, string message = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"Expected '{expected}' but was '{actual}'. {message}");
    }

    public static void AreNotEqual<T>(T expected, T actual, string message = "")
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"Expected not equal to '{expected}' but was '{actual}'. {message}");
    }

    public static void IsNotNull(object? value, string message = "")
    {
        if (value == null)
            throw new AssertionException($"Expected not null but was null. {message}");
    }

    public static void IsNull(object? value, string message = "")
    {
        if (value != null)
            throw new AssertionException($"Expected null but was '{value}'. {message}");
    }

    public static void Contains(string expectedSubstring, string actualString, string message = "")
    {
        ArgumentNullException.ThrowIfNull(actualString);
        ArgumentNullException.ThrowIfNull(expectedSubstring);
        if (!actualString.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new AssertionException($"Expected string to contain '{expectedSubstring}' but was '{actualString}'. {message}");
    }

    public static void DoesNotContain(string expectedSubstring, string actualString, string message = "")
    {
        ArgumentNullException.ThrowIfNull(actualString);
        ArgumentNullException.ThrowIfNull(expectedSubstring);
        if (actualString.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new AssertionException($"Expected string not to contain '{expectedSubstring}' but was '{actualString}'. {message}");
    }

    public static void Throws<T>(Action action, string message = "") where T : Exception
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            throw new AssertionException($"Expected exception of type {typeof(T).Name} but no exception was thrown. {message}");
        }
        catch (T)
        {
            // Expected exception type caught
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected exception of type {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}. {message}");
        }
    }

    public static void GreaterThan<T>(T expected, T actual, string message = "") where T : IComparable<T>
    {
        if (actual.CompareTo(expected) <= 0)
            throw new AssertionException($"Expected greater than '{expected}' but was '{actual}'. {message}");
    }

    public static void LessThan<T>(T expected, T actual, string message = "") where T : IComparable<T>
    {
        if (actual.CompareTo(expected) >= 0)
            throw new AssertionException($"Expected less than '{expected}' but was '{actual}'. {message}");
    }

    public static void DoesNotThrow(Action action, string message = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected no exception but got {ex.GetType().Name}: {ex.Message}. {message}");
        }
    }

    public static void Fail(string message) => throw new AssertionException(message);
}

public class AssertionException(string message) : Exception(message);
