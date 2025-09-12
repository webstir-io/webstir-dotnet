using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Middleware;

public sealed class EarlyHintsMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (TryGetPageName(context, out string? pageName) && !string.IsNullOrEmpty(pageName))
        {
            List<string> linkHints = BuildLinkHints(context, pageName!);
            if (linkHints.Count > 0)
            {
                TrySendEarlyHints(context, linkHints);
                context.Response.OnStarting(() =>
                {
                    foreach (string link in linkHints)
                    {
                        context.Response.Headers.Append("Link", new StringValues(link));
                    }
                    return Task.CompletedTask;
                });
            }
        }

        await _next(context);
    }

    private static bool TryGetPageName(HttpContext context, out string? pageName)
    {
        pageName = null;
        string? path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Expecting /pages/{page}/index.html after rewrite
        const string pagesPrefix = "/pages/";
        const string indexFile = "/index.html";
        if (path.StartsWith(pagesPrefix, StringComparison.Ordinal) && path.EndsWith(indexFile, StringComparison.OrdinalIgnoreCase))
        {
            string mid = path[pagesPrefix.Length..^indexFile.Length];
            if (!string.IsNullOrWhiteSpace(mid))
            {
                pageName = mid.Trim('/');
                return true;
            }
        }

        return false;
    }

    private static List<string> BuildLinkHints(HttpContext context, string pageName)
    {
        List<string> links = [];

        IWebHostEnvironment env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        string pageRoot = Path.Combine(env.WebRootPath, "pages", pageName);

        string cssRel = $"/pages/{pageName}/index.css";
        string cssPhy = Path.Combine(pageRoot, "index.css");
        if (File.Exists(cssPhy))
        {
            links.Add(FormattableString.Invariant($"<{cssRel}>; rel=preload; as=style; fetchpriority=high"));
        }

        string jsRel = $"/pages/{pageName}/index.js";
        string jsPhy = Path.Combine(pageRoot, "index.js");
        if (File.Exists(jsPhy))
        {
            links.Add(FormattableString.Invariant($"<{jsRel}>; rel=modulepreload; fetchpriority=high"));
        }

        return links;
    }

    private static void TrySendEarlyHints(HttpContext context, List<string> linkHints)
    {
        try
        {
            Type? featureType = Type.GetType("Microsoft.AspNetCore.Http.Features.IHttpResponseEarlyHintsFeature, Microsoft.AspNetCore.Http.Abstractions", throwOnError: false)
                ?? Type.GetType("Microsoft.AspNetCore.Http.Features.IHttpResponseEarlyHintsFeature", throwOnError: false);
            if (featureType is null)
            {
                return;
            }

            object? feature = context.Features[featureType];
            if (feature is null)
            {
                return;
            }

            MethodInfo? sendMethod = featureType.GetMethod("SendEarlyHints");
            if (sendMethod is null)
            {
                return;
            }

            HeaderDictionary headers = [];
            foreach (string link in linkHints)
            {
                headers.Append("Link", new StringValues(link));
            }
            sendMethod.Invoke(feature, [headers]);
        }
        catch
        {
            // Best-effort: ignore if not supported
        }
    }
}
