namespace Utilities.Process;

public readonly record struct ProcessOutput(ProcessOutputStream Stream, string Data);
