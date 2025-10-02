namespace Framework.Packaging;

public interface IPackageEnsureResult
{
    bool DependencyUpdated
    {
        get;
    }
    bool VersionMismatch
    {
        get;
    }
    string? InstalledVersion
    {
        get;
    }
}
