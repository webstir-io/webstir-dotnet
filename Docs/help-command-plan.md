# Help Command Implementation Plan

## Overview
Webstir currently lacks a help command, making it difficult for users to discover available commands and their usage. This plan outlines how to implement a comprehensive help system.

## Command Structure

```bash
webstir help                    # Show general help with all commands
webstir --help                  # Same as above
webstir -h                      # Same as above
webstir help <command>          # Show detailed help for specific command
webstir <command> --help        # Show help for specific command
```

## Implementation Plan

### 1. Help Data Structure

Create a `CommandHelp` class to store command information:

```csharp
public class CommandHelp
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Usage { get; set; }
    public List<string> Examples { get; set; }
    public List<CommandOption> Options { get; set; }
}

public class CommandOption
{
    public string Name { get; set; }
    public string Short { get; set; }
    public string Description { get; set; }
    public string DefaultValue { get; set; }
}
```

### 2. Command Registry

Create a dictionary of all available commands:

```csharp
private static readonly Dictionary<string, CommandHelp> Commands = new()
{
    ["init"] = new CommandHelp
    {
        Name = "init",
        Description = "Initialize a new webstir project",
        Usage = "webstir init [options]",
        Examples = new List<string>
        {
            "webstir init                    # Create a full-stack project",
            "webstir init --client-only      # Create a client-only project",
            "webstir init --server-only      # Create a server-only project"
        },
        Options = new List<CommandOption>
        {
            new() { Name = "--client-only", Description = "Create a client-side only project" },
            new() { Name = "--server-only", Description = "Create a server-side only project" },
            new() { Name = "--no-shared", Description = "Skip creating shared types folder" }
        }
    },
    // ... other commands
};
```

### 3. Help Output Format

#### General Help (webstir help)
```
Webstir - Modern web development build tool

Usage: webstir [command] [options]

Commands:
  init          Initialize a new webstir project
  add           Add a new page to your project
  build         Build the project once
  watch         Build and watch for changes (default)
  publish       Create production build
  help          Show this help message

Run 'webstir help <command>' for more information on a specific command.
```

#### Command-Specific Help (webstir help init)
```
Initialize a new webstir project

Usage: webstir init [options]

Options:
  --client-only     Create a client-side only project
  --server-only     Create a server-side only project
  --fullstack       Create a full-stack project (default)
  --no-shared       Skip creating shared types folder

Examples:
  webstir init                    # Create a full-stack project
  webstir init --client-only      # Create a client-only project
  webstir init --server-only      # Create a server-only project
  webstir init --no-shared        # Skip shared types folder
```

### 4. Runner.cs Modifications

Update the command handling to support help:

```csharp
public void Run(string[] args)
{
    var command = args.Length > 0 ? args[0].ToLower() : "";
    
    // Check for help flags
    if (command == "help" || command == "--help" || command == "-h")
    {
        if (args.Length > 1)
            ShowCommandHelp(args[1]);
        else
            ShowGeneralHelp();
        return;
    }
    
    // Check for command-specific help
    if (args.Length > 1 && (args[1] == "--help" || args[1] == "-h"))
    {
        ShowCommandHelp(command);
        return;
    }
    
    // Existing command handling...
}
```

### 5. Error Message Improvements

Update error messages to be more helpful:

```csharp
default:
    Console.WriteLine($"Unknown command '{command}'");
    Console.WriteLine();
    Console.WriteLine("Run 'webstir help' to see available commands.");
    return; // Don't run build on unknown commands
```

### 6. Color and Formatting

Add console colors for better readability:
- Command names: Cyan
- Options: Yellow
- Descriptions: Default
- Examples: Gray

```csharp
Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("  init");
Console.ResetColor();
Console.WriteLine("          Initialize a new webstir project");
```

## Benefits

1. **Discoverability**: Users can easily find available commands
2. **Self-Documenting**: No need to search external documentation
3. **Consistency**: Standardized help format across all commands
4. **Better UX**: Clear error messages guide users to help
5. **Extensibility**: Easy to add help for new commands

## Future Enhancements

1. Add version flag (`webstir --version`)
2. Add verbose flag for detailed output
3. Interactive mode for command selection
4. Shell completion scripts generation
5. Man page generation for Unix systems

## Testing

1. Test all help command variations work correctly
2. Verify help shows for unknown commands
3. Ensure command-specific help is accurate
4. Test color output on different terminals
5. Verify help text fits standard terminal widths (80 chars)