namespace Utilities.ProcessRunner;

public readonly record struct ProcessOutput(ProcessOutputStream Stream, string Data);
