namespace Framework.Packaging;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

internal static class PackageTarballManager
{
    public static async Task<string> EnsureTarballAsync(IPackageWorkspace workspace, FrameworkPackageMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        string destinationDirectory = workspace.WebstirPath;
        Directory.CreateDirectory(destinationDirectory);

        string destinationPath = metadata.GetWorkspaceTarballPath(workspace);

        if (File.Exists(destinationPath))
        {
            if (IsValid(destinationPath, metadata.Tarball))
            {
                return destinationPath;
            }

            TryDelete(destinationPath);
        }

        using (Stream source = metadata.OpenTarballStream())
        {
            using FileStream destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);
        }

        if (!IsValid(destinationPath, metadata.Tarball))
        {
            TryDelete(destinationPath);
            throw new InvalidOperationException($"Embedded tarball for {metadata.Name} failed validation.");
        }

        return destinationPath;
    }

    private static bool IsValid(string path, FrameworkPackageTarballMetadata tarball)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        FileInfo info = new(path);
        if (info.Length != tarball.Size)
        {
            return false;
        }

        string hash = ComputeSha256(path);
        return string.Equals(hash, tarball.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // ignore cleanup failures
        }
        catch (UnauthorizedAccessException)
        {
            // ignore cleanup failures
        }
    }
}
