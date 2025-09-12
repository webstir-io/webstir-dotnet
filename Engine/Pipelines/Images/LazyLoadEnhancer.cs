using System;
using Engine.Pipelines.Images.Common;

namespace Engine.Pipelines.Images;

public static class LazyLoadEnhancer
{

    public static string AddLazyLoading(string html, int aboveFoldCharBudget = 1200, int noLazyFirstImages = 1)
    {
        ArgumentNullException.ThrowIfNull(html);

        int imageCount = 0;
        return ImagesRegex.ImgTag().Replace(html, match =>
        {
            string tag = match.Value;
            imageCount++;

            if (ImagesRegex.LoadingAttr().IsMatch(tag))
            {
                return tag;
            }

            if (match.Index < aboveFoldCharBudget && imageCount <= noLazyFirstImages)
            {
                return tag;
            }

            int insertAt = tag.LastIndexOf('>');
            if (insertAt <= 0)
            {
                return tag;
            }

            return tag.Insert(insertAt, " loading=\"lazy\" fetchpriority=\"low\"");
        });
    }
}
