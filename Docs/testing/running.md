# Running Tests

The current runner remains the source of truth; these commands work today and will continue to work with the suite-based structure.

## Quick vs Full
- Quick (default): runs fast, essential cases only.
- Full: includes longer or more exhaustive checks (e.g., watch, deeper publish validations).

```bash
# Quick (default)
dotnet run --project Tests

# Full suite
dotnet run --project Tests -- --full

# Force quick even if env requests full
dotnet run --project Tests -- --quick

# Run a specific workflow (name match is substring)
dotnet run --project Tests -- test publish
# or
dotnet run --project Tests -- test build
```

Notes:
- The CLI help for the test runner is implemented in `Tests/Program.cs`.
- `TestMode.IsFull` gate controls which `ITestCase` objects run.
- Each workflow’s `*Tests` suite filters by `TestCategory` (Quick or Full).
