using System;

namespace Engine.Models;

public enum ClientNotificationType
{
    BuildStarting,
    BuildSucceeded,
    BuildFailed,
    Reload
}

public enum FileChangeType
{
    Modified,
    Created,
    Deleted,
    Renamed
}

public readonly record struct FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    DateTime Timestamp);
