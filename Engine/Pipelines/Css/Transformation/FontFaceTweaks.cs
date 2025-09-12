using System;
using Engine.Pipelines.Css.Common;

namespace Engine.Pipelines.Css.Transformation;

public static class FontFaceTweaks
{

    public static string EnsureFontDisplaySwap(string css)
    {
        ArgumentNullException.ThrowIfNull(css);

        return CssRegex.FontFaceBlock().Replace(css, match =>
        {
            string body = match.Groups["body"].Value;
            if (CssRegex.FontDisplayDecl().IsMatch(body))
            {
                return match.Value;
            }

            string updatedBody = body.TrimEnd();
            if (updatedBody.Length > 0 && !updatedBody.EndsWith(';'))
            {
                updatedBody += ";";
            }
            updatedBody += "font-display: swap;";
            return match.Value.Replace(body, updatedBody);
        });
    }
}