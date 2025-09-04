using System.Collections.Generic;

namespace Engine.Pipelines.JavaScript.Models;

public class JsModuleInfo
{
    public required string FilePath
    {
        get; set;
    }
    public required string Content
    {
        get; set;
    }
    public JsModuleType Type
    {
        get; set;
    }
    public List<JsImportStatement> Imports { get; set; } = [];
    public List<JsExportStatement> Exports { get; set; } = [];
}
