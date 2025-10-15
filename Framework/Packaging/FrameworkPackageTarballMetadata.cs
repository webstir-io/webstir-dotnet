namespace Framework.Packaging;

using System;
using System.IO;
using System.Reflection;

public readonly record struct FrameworkPackageTarballMetadata(
    string FileName,
    string RepositoryPath,
    string Sha256,
    long Size)
{
    private const string ResourcePrefix = "Framework.Resources.webstir";

    internal string ResourceName => $"{ResourcePrefix}.{FileName}";

    internal Stream OpenStream()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded tarball resource '{ResourceName}' not found for {FileName}.");
        }

        return stream;
    }

    internal string WorkspaceSpecifier => $"file:./.webstir/{FileName}";
}
