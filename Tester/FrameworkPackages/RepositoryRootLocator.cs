using System;
using System.IO;
using Tester.Infrastructure;

namespace Tester.FrameworkPackages;

internal static class RepositoryRootLocator
{
    public static string Resolve()
    {
        string current = Paths.RepositoryRoot;
        while (!Directory.Exists(Path.Combine(current, "Framework")))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null)
            {
                throw new InvalidOperationException("Unable to locate Framework directory from test context.");
            }

            current = parent.FullName;
        }

        return current;
    }
}
