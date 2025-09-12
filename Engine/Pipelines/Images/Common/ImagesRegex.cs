using System.Text.RegularExpressions;

namespace Engine.Pipelines.Images.Common;

public static partial class ImagesRegex
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ImgTag();

    [GeneratedRegex(@"\bloading\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex LoadingAttr();

    [GeneratedRegex(@"\bfetchpriority\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex FetchPriorityAttr();
}