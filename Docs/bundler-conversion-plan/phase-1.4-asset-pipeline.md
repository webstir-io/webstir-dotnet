# Phase 1.4: Asset Pipeline

## Overview
Implement a comprehensive asset pipeline with fingerprinting, content hashing, and optimization for images, fonts, and other static assets.

## Goals
- Asset fingerprinting with content hashes
- Asset manifest generation
- Inline optimization for small files
- Image compression
- Font subsetting

## Technical Design

### Core Components

#### 1. Asset Fingerprinter
```csharp
public class AssetFingerprinter
{
    public async Task<FingerprintedAsset> ProcessAssetAsync(string filePath)
    {
        var content = await File.ReadAllBytesAsync(filePath);
        var hash = GenerateContentHash(content);
        var extension = Path.GetExtension(filePath);
        var name = Path.GetFileNameWithoutExtension(filePath);
        
        var fingerprintedName = $"{name}.{hash}{extension}";
        
        return new FingerprintedAsset
        {
            OriginalPath = filePath,
            FingerprintedName = fingerprintedName,
            Hash = hash,
            Content = content,
            Size = content.Length
        };
    }
    
    private string GenerateContentHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToBase64String(hash)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 8);
    }
}
```

#### 2. Asset Manifest
```csharp
public class AssetManifest
{
    private readonly Dictionary<string, AssetEntry> _entries = new();
    
    public class AssetEntry
    {
        public string Original { get; set; }
        public string Fingerprinted { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
        public string Integrity { get; set; }
    }
    
    public void AddAsset(FingerprintedAsset asset)
    {
        _entries[asset.OriginalPath] = new AssetEntry
        {
            Original = asset.OriginalPath,
            Fingerprinted = asset.FingerprintedName,
            Hash = asset.Hash,
            Size = asset.Size,
            Integrity = GenerateIntegrity(asset.Content)
        };
    }
    
    public async Task SaveManifestAsync(string path)
    {
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(path, json);
    }
}
```

#### 3. Inline Asset Optimizer
```csharp
public class InlineAssetOptimizer
{
    private const int MaxInlineSize = 4096; // 4KB threshold
    
    public string ProcessHtml(string html, AssetManifest manifest)
    {
        // Replace small images with data URIs
        html = InlineSmallImages(html, manifest);
        
        // Inline critical CSS
        html = InlineCriticalCss(html, manifest);
        
        // Inline small scripts
        html = InlineSmallScripts(html, manifest);
        
        return html;
    }
    
    private string InlineSmallImages(string html, AssetManifest manifest)
    {
        var pattern = @"<img[^>]+src=[""']([^""']+)[""'][^>]*>";
        
        return Regex.Replace(html, pattern, match =>
        {
            var src = match.Groups[1].Value;
            if (manifest.TryGetAsset(src, out var asset) && asset.Size < MaxInlineSize)
            {
                var dataUri = ConvertToDataUri(asset);
                return match.Value.Replace(src, dataUri);
            }
            return match.Value;
        });
    }
}
```

## Implementation Steps

### Step 1: Content Hash Generation
### Step 2: Asset Manifest Creation
### Step 3: Inline Optimization
### Step 4: Image Compression
### Step 5: Font Subsetting

## Success Criteria
- ✅ Unique hashes for all assets
- ✅ Manifest maps original to hashed
- ✅ Small files inlined automatically
- ✅ Images optimized without quality loss
- ✅ Fonts subset to used characters