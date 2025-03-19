namespace CLI.Models;

public class Dependency
{
    public required string Filepath { get; set; }
    public required string Name { get; set; }
    public required string Content { get; set; }
}