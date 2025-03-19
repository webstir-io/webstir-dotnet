using CLI.Models;

namespace CLI.Bundlers;

public class ScriptBundler
{
    public string Bundle(string entryFilepath)
    {
        List<ModuleImport> imports = GetImports(entryFilepath);

        throw new NotImplementedException();
    }

    private static List<ModuleImport> GetImports(string filepath)
    {
        var fileLines = File.ReadAllLines(filepath);
        var imports = new List<ModuleImport>();
        var insideMultiLineComment = false;
        foreach (var line in fileLines)
        {
            while(insideMultiLineComment)
            {
                if (line.EndsWith("*/"))
                {
                    insideMultiLineComment = false;
                    break;
                }
            }

            if (line.StartsWith("/*"))
            {
                insideMultiLineComment = true;
                continue;
            }

            if (line.StartsWith("//"))
                continue;

            if (line.Contains("import"))
            {
                imports.Add(new ModuleImport(line));
            }
        }

        return imports;
    }
}