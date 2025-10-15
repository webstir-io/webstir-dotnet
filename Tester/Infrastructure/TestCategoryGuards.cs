namespace Tester.Infrastructure;

public static class TestCategoryGuards
{
    public static bool ShouldRun(TestCategory category) =>
        category != TestCategory.Full || TestMode.IsFull;
}
