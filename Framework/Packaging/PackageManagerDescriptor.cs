using System;

namespace Framework.Packaging;

public readonly record struct PackageManagerDescriptor(
    PackageManagerKind Kind,
    string Executable,
    string DisplayName,
    string? Version)
{
    public static PackageManagerDescriptor Create(PackageManagerKind kind, string executable, string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        string displayName = string.IsNullOrWhiteSpace(version)
            ? executable
            : $"{executable}@{version}";

        return new PackageManagerDescriptor(kind, executable, displayName, version);
    }
}
