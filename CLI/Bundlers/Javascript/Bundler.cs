using System.Text.RegularExpressions;
using System.Text;

namespace CLI.Bundlers.Javascript;

public static class Bundler
{
    // All methods related to manual bundling (Bundle, ParseDependencies, ResolveDependencyPath, HandleUnsupportedDependency)
    // have been removed. TypeScript compilation (tsc) now produces ES modules,
    // and module loading is handled natively by the browser.
    // This class is retained for potential future JavaScript processing tasks if any arise.
}