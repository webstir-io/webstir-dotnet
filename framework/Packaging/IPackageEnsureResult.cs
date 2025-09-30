using System;

namespace Framework.Packaging;

public interface IPackageEnsureResult
{
    bool ToolsAdded { get; }
    bool DependencyUpdated { get; }
    bool TarballUpdated { get; }
    bool VersionMismatch { get; }
    string? InstalledVersion { get; }
}
