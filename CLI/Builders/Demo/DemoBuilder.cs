using CLI.Interfaces;
using CLI.Models;
using CLI.Helpers;
using System.Reflection;

namespace CLI.Builders.Demo;

public class DemoBuilder : ITemplateBuilder
{
    private readonly IEnumerable<IFileWorker> _fileWorkers;
    
    public DemoBuilder(IEnumerable<IFileWorker> fileWorkers)
    {
        _fileWorkers = fileWorkers;
    }
    
    public string TemplateName => "demo";
    public string Description => "Creates a demo application showcasing all webstir features";
    
    public void CreateTemplate(string directory)
    {
        Console.WriteLine($"Creating webstir demo in {directory}...");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Save current directory
        var originalDirectory = Directory.GetCurrentDirectory();
        
        try
        {
            // Change to target directory
            Directory.SetCurrentDirectory(directory);
            
            // Initialize basic webstir project structure using existing workers
            Console.WriteLine("Initializing webstir project...");
            foreach (var worker in _fileWorkers)
            {
                worker.Init(ProjectMode.Fullstack);
            }
            
            // Copy demo template files
            Console.WriteLine("Adding demo files...");
            CopyDemoFiles();
            
            Console.WriteLine("✓ Demo application created successfully!");
        }
        finally
        {
            // Restore original directory
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }
    
    private void CopyDemoFiles()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePrefix = "CLI.Builders.Demo.";
        
        // Get all embedded resources that start with our prefix
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix))
            .ToList();
        
        foreach (var resourceName in resources)
        {
            // Convert resource name to file path
            // e.g., "CLI.Builders.Demo.src.client.app.app.html" -> "src/client/app/app.html"
            var relativePath = resourceName.Substring(resourcePrefix.Length);
            
            // Special handling for README.md
            if (relativePath == "README.md")
            {
                // Keep as is
            }
            else
            {
                // Find the last dot that represents the file extension
                var lastDotIndex = relativePath.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    // Everything before the last dot uses path separator
                    var pathPart = relativePath.Substring(0, lastDotIndex).Replace('.', Path.DirectorySeparatorChar);
                    var extension = relativePath.Substring(lastDotIndex); // includes the dot
                    relativePath = pathPart + extension;
                }
                else
                {
                    // No extension, just convert all dots to path separators
                    relativePath = relativePath.Replace('.', Path.DirectorySeparatorChar);
                }
            }
            
            // Create directory if needed
            var targetDir = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            
            // Write the resource to file
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var fileStream = File.Create(relativePath);
                stream.CopyTo(fileStream);
            }
        }
    }
}