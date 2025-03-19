namespace CLI.Models
{
    public enum ModuleImportType
    {
        DefaultExport,
        NamedOnly,
        HasAlias,
        Namespace,
        SideEffect
    }

    public class ModuleImport
    {
        public string SpecifierElement { get; set; } = string.Empty;
        public string ModuleElement { get; set; } = string.Empty;
        public ModuleImportType ImportType { get; set; }
        public bool IsInternal => ModuleElement.StartsWith('.') || ModuleElement.StartsWith('$');

        public ModuleImport(string importStatement)
        {
            ParseImportStatement(importStatement);
            IdentifyImportType();        
        }

        public List<string> Specifiers()
        {
            return ImportType switch
            {
                ModuleImportType.DefaultExport => [ModuleElement],
                ModuleImportType.NamedOnly => [.. SpecifierElement
                        .Replace("{", "")
                        .Replace("}", "")
                        .Split(",")
                        .Select(x => x.Trim())],
                ModuleImportType.HasAlias => [.. SpecifierElement
                        .Replace("{", "")
                        .Replace("}", "")
                        .Split(",")
                        .Select(x => x.Contains("as")
                            ? x.Split("as")[1].Trim()
                            : x.Trim())],
                ModuleImportType.Namespace => ["*"],
                ModuleImportType.SideEffect => [],
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private void ParseImportStatement(string importStatement)
        {
            var importElements = importStatement.Split("from");
            if (importElements.Length == 1)
            {
                ModuleElement = importElements[0].Trim();
                return;
            }

            if (importElements.Length == 2)
            {
                SpecifierElement = importElements[0].Trim();
                ModuleElement = importElements[1].Trim();
                if (string.IsNullOrWhiteSpace(SpecifierElement) 
                    || string.IsNullOrWhiteSpace(ModuleElement))
                    throw new ArgumentException("Invalid import statement");
                return;
            }

            throw new ArgumentException("Invalid import statement");
        }

        private void IdentifyImportType()
        {
            if (SpecifierElement.Contains('*'))
            {
                ImportType = ModuleImportType.Namespace;
            }
            else if (SpecifierElement.Contains('{'))
            {
                ImportType = SpecifierElement.Contains("as") 
                    ? ModuleImportType.HasAlias 
                    : ModuleImportType.NamedOnly;
            }
            else if (string.IsNullOrEmpty(SpecifierElement))
            {
                ImportType = ModuleImportType.SideEffect;
            }
            else
            {
                ImportType = ModuleImportType.DefaultExport;
            }
        }
    }
}