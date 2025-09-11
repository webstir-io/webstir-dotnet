using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Engine.Pipelines.Images;

public static class SvgSanitizer
{
    // Allow core drawing + gradients/filters; block scripting and foreign content
    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "g", "defs", "use",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "linearGradient", "radialGradient", "stop",
        "filter", "feGaussianBlur", "feOffset", "feColorMatrix", "feMerge", "feMergeNode", "feBlend",
        "clipPath", "mask", "pattern", "text", "tspan"
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "class", "style",
        "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry",
        "d", "points", "width", "height", "viewBox", "fill", "stroke", "stroke-width",
        "transform", "opacity", "clip-path", "mask", "href", "xlink:href",
        "gradientUnits", "gradientTransform", "offset", "stop-color", "stop-opacity",
        "filterUnits", "stdDeviation", "in", "in2", "result", "type", "operator"
    };

    public static string Sanitize(string svgContent)
    {
        ArgumentNullException.ThrowIfNull(svgContent);

        try
        {
            XDocument doc = XDocument.Parse(svgContent, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            XElement? root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            {
                return svgContent; // Not an SVG; return as-is
            }

            SanitizeElementRecursive(root);
            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            // In case of parse error, return original to avoid breaking builds
            return svgContent;
        }
    }

    private static void SanitizeElementRecursive(XElement element)
    {
        if (!AllowedElements.Contains(element.Name.LocalName))
        {
            element.Remove();
            return;
        }

        // Remove event handler attributes and non-allowlisted attributes
        List<XAttribute> toRemove = [];
        foreach (XAttribute attr in element.Attributes())
        {
            string name = attr.Name.LocalName;
            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(attr);
                continue;
            }

            if (!AllowedAttributes.Contains(name))
            {
                toRemove.Add(attr);
            }
        }

        foreach (XAttribute attr in toRemove)
        {
            attr.Remove();
        }

        foreach (XElement child in element.Elements().ToList())
        {
            SanitizeElementRecursive(child);
        }
    }
}

