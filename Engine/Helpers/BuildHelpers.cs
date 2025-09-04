using System;

namespace Engine.Helpers;

public static class BuildHelpers
{
    public static bool ContainsBuildFolder(string path, string folderName)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folderName))
            return false;

        return path.Contains($"/{Folders.Src}/{folderName}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"\\{Folders.Src}\\{folderName}", StringComparison.OrdinalIgnoreCase);
    }
}
