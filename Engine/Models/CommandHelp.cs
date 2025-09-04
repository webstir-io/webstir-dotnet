using System.Collections.Generic;

namespace Engine.Models;

public class CommandHelp
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = [];
    public List<CommandOption> Options { get; set; } = [];
}

public class CommandOption
{
    public string Name { get; set; } = string.Empty;
    public string Short { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
}
