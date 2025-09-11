namespace Engine.Models;

public sealed class ImageAsset
{
    public required string SourcePath
    {
        get; init;
    }
    public int Width
    {
        get; init;
    }
    public int Height
    {
        get; init;
    }
    public bool HasWebP
    {
        get; init;
    }
    public bool HasAvif
    {
        get; init;
    }
}

