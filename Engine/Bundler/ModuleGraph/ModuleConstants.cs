namespace Engine.Bundler.ModuleGraph;

public static class ModuleConstants
{
    public static class Prefixes
    {
        public const string Node = "node:";
        public const string Relative = "./";
        public const string ParentRelative = "../";
    }
    
    public static class Extensions
    {
        public const string TypeScript = ".ts";
        public const string JavaScript = ".js";
        public const string ModuleJs = ".mjs";
        public const string Json = ".json";
    }
    
    public static class PackageJsonFields
    {
        public const string Main = "main";
        public const string Module = "module";
    }
}