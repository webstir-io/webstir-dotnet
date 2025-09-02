# Target Test Folder Structure

The structure below keeps each workflow self-contained and discoverable.

```
Tests/
  Framework/
    Assert.cs
    ProcessRunner.cs
    ITestRunner.cs
    ITestSuite.cs
    ITestCase.cs            # new
    TestCaseBase.cs         # new
    TestCaseContext.cs      # new
    TestCategory.cs         # new

  # Optional: Add helper files later if needed; otherwise keep helpers per-workflow or in Framework

  Workflows/
    Init/
      InitTests.cs                # implements ITestSuite
      InitCreatesDefaultProject.cs
      InitCreatesNamedProject.cs
      ...
    Build/
      BuildTests.cs               # implements ITestSuite
      BuildRunsWithoutErrors.cs
      MissingAppHtmlShowsError.cs
      ...
    Publish/
      PublishTests.cs             # implements ITestSuite
      PublishRunsWithoutErrors.cs
      ClientArtifactsExist.cs
      JsIsMinified.cs
      CssIsMinified.cs
      HtmlWhitespaceCollapsed.cs
      ManifestIntegrity.cs
    Watch/
      WatchTests.cs               # implements ITestSuite
      WatchStartsAndSignalsReady.cs
    Help/
      HelpTests.cs                # implements ITestSuite
      HelpShowsKeyCommands.cs

Program.cs                  # unchanged API; registers suites
```

Notes:
- Each workflow’s `*Tests.cs` file is the only `ITestSuite` in that folder.
- Individual test case files live directly in the workflow folder (no nested `Tests/`).
- Existing `BaseTest` patterns are absorbed into `TestCaseBase` and shared helpers.
