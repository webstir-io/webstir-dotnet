namespace Engine.Models;

public readonly record struct WorkspaceProfile(
    WorkspaceMode Mode,
    bool HasFrontend,
    bool HasBackend)
{
    public static WorkspaceProfile Full => new(WorkspaceMode.Full, true, true);
    public static WorkspaceProfile Ssg => new(WorkspaceMode.Ssg, true, false);
    public static WorkspaceProfile Spa => new(WorkspaceMode.Spa, true, false);
    public static WorkspaceProfile Api => new(WorkspaceMode.Api, false, true);
}
