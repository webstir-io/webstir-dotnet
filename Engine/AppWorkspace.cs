using System;
using System.IO;
using System.Text.Json;
using Engine.Extensions;
using Engine.Models;

namespace Engine;

public class AppWorkspace
{
    private string _workingFolder = string.Empty;

    public void Initialize(string workingFolder) => _workingFolder = workingFolder;

    public string WorkingPath => Directory.CreateDirectory(_workingFolder).FullName;
    public string WorkspaceName => Path.GetFileName(WorkingPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public string NodeModulesPath => WorkingPath.CreateSubDirectory(Folders.NodeModules);
    public string WebstirPath => WorkingPath.Combine(Folders.Webstir);
    public string FrontendManifestPath => WebstirPath.Combine(Files.FrontendManifestJson);
    public string BackendManifestPath => WebstirPath.Combine(Files.BackendManifestJson);

    public string SrcPath => WorkingPath.CreateSubDirectory(Folders.Src);
    public string BuildPath => WorkingPath.CreateSubDirectory(Folders.Build);
    public string DistPath => WorkingPath.CreateSubDirectory(Folders.Dist);

    public string FrontendPath => SrcPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendAppPath => FrontendPath.CreateSubDirectory(Folders.App);
    public string FrontendPagesPath => FrontendPath.CreateSubDirectory(Folders.Pages);
    public string FrontendImagesPath => FrontendPath.CreateSubDirectory(Folders.Images);
    public string FrontendFontsPath => FrontendPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendMediaPath => FrontendPath.CreateSubDirectory(Folders.Media);
    public string FrontendBuildPath => BuildPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendBuildAppPath => FrontendBuildPath.CreateSubDirectory(Folders.App);
    public string FrontendBuildPagesPath => FrontendBuildPath.CreateSubDirectory(Folders.Pages);
    public string FrontendBuildImagesPath => FrontendBuildPath.CreateSubDirectory(Folders.Images);
    public string FrontendBuildFontsPath => FrontendBuildPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendBuildMediaPath => FrontendBuildPath.CreateSubDirectory(Folders.Media);
    public string FrontendDistPath => DistPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendDistImagesPath => FrontendDistPath.CreateSubDirectory(Folders.Images);
    public string FrontendDistFontsPath => FrontendDistPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendDistMediaPath => FrontendDistPath.CreateSubDirectory(Folders.Media);
    public string FrontendDistPagesPath => FrontendDistPath.CreateSubDirectory(Folders.Pages);
    public string FrontendDistAppPath => FrontendDistPath.CreateSubDirectory(Folders.App);

    public string BackendPath => WorkingPath.Combine(Folders.Src, Folders.Backend);
    public string BackendBuildPath => BuildPath.Combine(Folders.Backend);
    public string BackendDistPath => DistPath.Combine(Folders.Backend);

    public string SharedPath => SrcPath.CreateSubDirectory(Folders.Shared);

    public WorkspaceProfile DetectWorkspaceProfile()
    {
        WorkspaceProfile? fromPackage = TryReadWorkspaceProfile();
        if (fromPackage is { } profileFromPackage)
        {
            return profileFromPackage;
        }

        return DetectProfileFromFolders();
    }

    private WorkspaceProfile DetectProfileFromFolders()
    {
        string clientPath = WorkingPath.Combine(Folders.Src, Folders.Frontend);
        string serverPath = WorkingPath.Combine(Folders.Src, Folders.Backend);

        bool hasClientDir = clientPath.Exists();
        bool hasServerDir = serverPath.Exists();

        return (hasClientDir, hasServerDir) switch
        {
            (true, true) => WorkspaceProfile.Full,
            (true, false) => WorkspaceProfile.Spa,
            (false, true) => WorkspaceProfile.Api,
            // When neither exists, assume frontend-only to avoid running backend by default.
            (false, false) => WorkspaceProfile.Spa
        };
    }

    private WorkspaceProfile? TryReadWorkspaceProfile()
    {
        string packageJsonPath = WorkingPath.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(packageJsonPath);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("webstir", out JsonElement webstir))
            {
                return null;
            }

            if (!webstir.TryGetProperty("mode", out JsonElement modeElement))
            {
                return null;
            }

            string? modeValue = modeElement.GetString();
            return modeValue?.ToLowerInvariant() switch
            {
                "ssg" => WorkspaceProfile.Ssg,
                "spa" => WorkspaceProfile.Spa,
                "api" => WorkspaceProfile.Api,
                "full" => WorkspaceProfile.Full,
                _ => null
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToDisplayPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        try
        {
            string fullFilePath = Path.GetFullPath(filePath);
            string fullWorkingPath = Path.GetFullPath(TrimEndingSeparators(WorkingPath));
            string relativePath = Path.GetRelativePath(fullWorkingPath, fullFilePath);

            if (IsOutsideWorkingFolder(relativePath))
            {
                return filePath;
            }

            string projectFolderName = Path.GetFileName(fullWorkingPath);
            if (string.IsNullOrWhiteSpace(projectFolderName))
            {
                return relativePath;
            }

            return Path.Combine(projectFolderName, relativePath);
        }
        catch (Exception)
        {
            return filePath;
        }
    }

    private static bool IsOutsideWorkingFolder(string relativePath)
    {
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return true;
        }

        return Path.IsPathRooted(relativePath);
    }

    private static string TrimEndingSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
