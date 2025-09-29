using System;

namespace Engine.Bridge.Packaging;

internal interface IPackageEnsureResult
{
    bool ToolsAdded { get; }
    bool DependencyUpdated { get; }
    bool TarballUpdated { get; }
    bool VersionMismatch { get; }
    string? InstalledVersion { get; }
}
