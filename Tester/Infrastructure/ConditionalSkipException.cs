using Xunit.Sdk;

namespace Tester.Infrastructure;

/// <summary>
/// Minimal skip exception so tests can bail out when prerequisites are missing.
/// </summary>
public sealed class ConditionalSkipException : XunitException
{
    public ConditionalSkipException(string message)
        : base(message)
    {
    }
}
