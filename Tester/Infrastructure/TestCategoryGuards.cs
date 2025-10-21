namespace Tester.Infrastructure;

public static class TestCategoryGuards
{
    public static bool ShouldRun(TestCategory category)
    {
        if (category != TestCategory.Full)
        {
            return true;
        }

        if (!TestMode.IsFull)
        {
            return false;
        }

        return WorkspaceManager.EnsureLocalPackagesReady();
    }
}
