## Summary

- What does this change do? Link issues/PRs where helpful.

## Checklist

- [ ] I ran `dotnet build` and addressed any compile errors
- [ ] I ran quick tests locally (`dotnet test Tester/Tester.csproj`)
- [ ] I updated docs/README if behavior changed

## CI Lanes

- Quick lane runs by default on PRs.
- To run the Full lane (more comprehensive, includes native image tooling and package sync/verify), add the label: `ci:full`.

> Tip: You can also trigger Full by pushing to `main` (runs automatically after merge).

