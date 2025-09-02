namespace Tests.Framework;

public interface ITestCase
{
    string Name { get; }
    TestCategory Category { get; }
    void Execute(TestCaseContext context);
}

