using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Interfaces;

namespace Engine.Workflows;

public sealed class AddRouteWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    private static readonly string[] AllowedMethods =
    {
        "GET",
        "HEAD",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS"
    };

    private static readonly string[] AllowedSchemaKinds =
    {
        "zod",
        "json-schema",
        "ts-rest"
    };

    public override string WorkflowName => Commands.AddRoute;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        string? name = filteredArgs.FirstOrDefault(arg => !arg.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddRoute} <name> [--method <METHOD>] [--path <path>] [--project <project>]");
        }

        string method = ParseOptionValue(filteredArgs, "--method")?.ToUpperInvariant() ?? "GET";
        if (!AllowedMethods.Contains(method))
        {
            string allowedList = string.Join(", ", AllowedMethods);
            throw new ArgumentException($"Invalid --method value '{method}'. Allowed values: {allowedList}.");
        }
        string path = ParseOptionValue(filteredArgs, "--path") ?? $"/api/{name}";
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        string? summary = NormalizeOptionalString(ParseOptionValue(filteredArgs, "--summary"));
        string? description = NormalizeOptionalString(ParseOptionValue(filteredArgs, "--description"));
        string[] tags = NormalizeTags(ParseOptionValue(filteredArgs, "--tags"));

        JsonObject? paramsSchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--params-schema"), "--params-schema");
        JsonObject? querySchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--query-schema"), "--query-schema");
        JsonObject? bodySchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--body-schema"), "--body-schema");
        JsonObject? headersSchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--headers-schema"), "--headers-schema");
        JsonObject? responseSchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--response-schema"), "--response-schema");
        JsonObject? responseHeadersSchema = ParseSchemaReference(ParseOptionValue(filteredArgs, "--response-headers-schema"), "--response-headers-schema");
        int? responseStatus = ParseStatusOption(ParseOptionValue(filteredArgs, "--response-status"), "--response-status");

        if ((responseHeadersSchema is not null || responseStatus.HasValue) && responseSchema is null)
        {
            throw new ArgumentException("--response-schema is required when setting response headers or status.");
        }

        bool useFastify = HasFlag(filteredArgs, "--fastify");
        UpdatePackageJsonWithRoute(
            name.Trim(),
            method.Trim(),
            path.Trim(),
            summary,
            description,
            tags,
            paramsSchema,
            querySchema,
            bodySchema,
            headersSchema,
            responseSchema,
            responseHeadersSchema,
            responseStatus);

        if (useFastify)
        {
            await ScaffoldFastifyRouteAsync(name.Trim(), method.Trim(), path.Trim());
        }

        Console.WriteLine($"Added route {method} {path} to package.json manifest{(useFastify ? " and scaffolded Fastify handler" : string.Empty)}.");
    }

    private static string? ParseOptionValue(string[] args, string option)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, option, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    return args[i + 1];
                }
                return null;
            }

            if (arg.StartsWith(option + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(option.Length + 1)..];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] args, string flag) => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeOptionalString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string[] NormalizeTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        string[] parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<string> tags = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string part in parts)
        {
            string tag = part.Trim();
            if (tag.Length == 0)
            {
                continue;
            }

            if (seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return tags.ToArray();
    }

    private void UpdatePackageJsonWithRoute(
        string name,
        string method,
        string path,
        string? summary,
        string? description,
        IReadOnlyList<string> tags,
        JsonObject? paramsSchema,
        JsonObject? querySchema,
        JsonObject? bodySchema,
        JsonObject? headersSchema,
        JsonObject? responseSchema,
        JsonObject? responseHeadersSchema,
        int? responseStatus)
    {
        string packageJsonPath = Path.Combine(Context.WorkingPath, Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            throw new FileNotFoundException($"{Files.PackageJson} not found in workspace root.", packageJsonPath);
        }

        JsonNode root = JsonNode.Parse(File.ReadAllText(packageJsonPath)) ?? new JsonObject();
        JsonObject pkg = root as JsonObject ?? new JsonObject();
        JsonObject webstir = pkg["webstir"] as JsonObject ?? new JsonObject();
        JsonObject moduleManifest = webstir["moduleManifest"] as JsonObject ?? new JsonObject();
        JsonArray routes = moduleManifest["routes"] as JsonArray ?? new JsonArray();

        JsonObject? routeObject = routes
            .Select(r => r as JsonObject)
            .FirstOrDefault(o => o is not null &&
                                 string.Equals((o["method"] as JsonValue)?.ToString(), method, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals((o["path"] as JsonValue)?.ToString(), path, StringComparison.Ordinal));

        if (routeObject is null)
        {
            routeObject = new JsonObject();
            routes.Add(routeObject);
        }

        routeObject["name"] = name;
        routeObject["method"] = method;
        routeObject["path"] = path;
        ApplyOptionalString(routeObject, "summary", summary);
        ApplyOptionalString(routeObject, "description", description);
        ApplyTags(routeObject, tags);
        ApplyInput(routeObject, paramsSchema, querySchema, bodySchema, headersSchema);
        ApplyOutput(routeObject, responseSchema, responseHeadersSchema, responseStatus);

        moduleManifest["routes"] = routes;
        webstir["moduleManifest"] = moduleManifest;
        pkg["webstir"] = webstir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        File.WriteAllText(packageJsonPath, pkg.ToJsonString(options));
    }

    private static void ApplyOptionalString(JsonObject target, string property, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            target[property] = value;
        }
        else
        {
            target.Remove(property);
        }
    }

    private static void ApplyTags(JsonObject target, IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            target.Remove("tags");
            return;
        }

        JsonArray tagArray = new();
        foreach (string tag in tags)
        {
            tagArray.Add(tag);
        }
        target["tags"] = tagArray;
    }

    private static void ApplyInput(JsonObject route, JsonObject? paramsSchema, JsonObject? querySchema, JsonObject? bodySchema, JsonObject? headersSchema)
    {
        JsonObject input = route["input"] as JsonObject ?? new JsonObject();

        ApplySchemaReference(input, "params", paramsSchema);
        ApplySchemaReference(input, "query", querySchema);
        ApplySchemaReference(input, "body", bodySchema);
        ApplySchemaReference(input, "headers", headersSchema);

        if (input.Count > 0)
        {
            route["input"] = input;
        }
        else
        {
            route.Remove("input");
        }
    }

    private static void ApplyOutput(JsonObject route, JsonObject? responseSchema, JsonObject? responseHeadersSchema, int? responseStatus)
    {
        if (responseSchema is null && responseHeadersSchema is null && !responseStatus.HasValue)
        {
            route.Remove("output");
            return;
        }

        JsonObject output = route["output"] as JsonObject ?? new JsonObject();

        if (responseSchema is not null)
        {
            output["body"] = responseSchema;
        }
        else
        {
            output.Remove("body");
        }

        if (responseStatus.HasValue)
        {
            output["status"] = responseStatus.Value;
        }
        else
        {
            output.Remove("status");
        }

        ApplySchemaReference(output, "headers", responseHeadersSchema);

        if (output.Count > 0)
        {
            route["output"] = output;
        }
        else
        {
            route.Remove("output");
        }
    }

    private static void ApplySchemaReference(JsonObject target, string property, JsonObject? value)
    {
        if (value is not null)
        {
            target[property] = value;
        }
        else
        {
            target.Remove(property);
        }
    }

    private static JsonObject? ParseSchemaReference(string? value, string flag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        string schemaKind = "zod";
        string remainder = trimmed;

        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
        {
            if (colonIndex == 0)
            {
                throw new ArgumentException($"Invalid {flag} value '{value}'. Missing schema name.");
            }

            schemaKind = trimmed[..colonIndex].Trim();
            remainder = trimmed[(colonIndex + 1)..];
        }

        string? source = null;
        int atIndex = remainder.IndexOf('@');
        if (atIndex >= 0)
        {
            source = remainder[(atIndex + 1)..].Trim();
            remainder = remainder[..atIndex];
        }

        string name = remainder.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"Invalid {flag} value '{value}'. Schema name is required.");
        }

        schemaKind = string.IsNullOrWhiteSpace(schemaKind) ? "zod" : schemaKind.Trim().ToLowerInvariant();
        if (!AllowedSchemaKinds.Contains(schemaKind))
        {
            string allowedKinds = string.Join(", ", AllowedSchemaKinds);
            throw new ArgumentException($"Invalid schema kind '{schemaKind}' in {flag}. Allowed kinds: {allowedKinds}.");
        }

        JsonObject reference = new()
        {
            ["kind"] = schemaKind,
            ["name"] = name
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            reference["source"] = source;
        }

        return reference;
    }

    private static int? ParseStatusOption(string? value, string flag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int status))
        {
            throw new ArgumentException($"Invalid {flag} value '{value}'. Expected an integer status code.");
        }

        if (status is < 100 or > 599)
        {
            throw new ArgumentException($"Invalid {flag} value '{value}'. Status must be between 100 and 599.");
        }

        return status;
    }

    private async Task ScaffoldFastifyRouteAsync(string name, string method, string path)
    {
        string serverDir = Path.Combine(Context.BackendPath, "server");
        string routesDir = Path.Combine(serverDir, "routes");
        Directory.CreateDirectory(routesDir);

        string routeFile = Path.Combine(routesDir, $"{name}.ts");
        if (!File.Exists(routeFile))
        {
            await File.WriteAllTextAsync(routeFile, BuildFastifyRouteTemplate(name, method, path));
        }

        string fastifyPath = Path.Combine(serverDir, "fastify.ts");
        if (File.Exists(fastifyPath))
        {
            TryPatchFastifyRegistration(fastifyPath, name);
        }
        else
        {
            Console.WriteLine($"Note: {Path.GetRelativePath(Context.WorkingPath, routeFile)} created. Import and register it from a Fastify server when ready.");
        }
    }

    private static string BuildFastifyRouteTemplate(string name, string method, string path)
    {
        string upper = method.ToUpperInvariant();
        return $$"""
// Generated by webstir add-route --fastify
export function register(app: import('fastify').FastifyInstance) {
  app.route({
    method: '{upper}',
    url: '{path}',
    handler: async (req, reply) => {
      reply.code(200).send({ ok: true, route: '{name}' });
    }
  });
}
""";
    }

    private static void TryPatchFastifyRegistration(string fastifyPath, string name)
    {
        string content = File.ReadAllText(fastifyPath);
        string importLine = $"import {{ register as register{ToPascal(name)} }} from './routes/{name}';";
        string callLine = $"  register{ToPascal(name)}(app);";

        if (!content.Contains(importLine, StringComparison.Ordinal))
        {
            // Insert import after Fastify import
            int anchor = content.IndexOf("import Fastify from 'fastify';", StringComparison.Ordinal);
            if (anchor >= 0)
            {
                int endOfLine = content.IndexOf('\n', anchor);
                if (endOfLine > 0)
                {
                    content = content.Insert(endOfLine + 1, importLine + "\n");
                }
            }
        }

        if (!content.Contains(callLine, StringComparison.Ordinal))
        {
            // Insert registration after app.get('/api/health') or after app creation
            int insertAt = content.IndexOf("app.get('/api/health'", StringComparison.Ordinal);
            if (insertAt >= 0)
            {
                int endOfLine = content.IndexOf('\n', insertAt);
                if (endOfLine > 0)
                {
                    content = content.Insert(endOfLine + 1, callLine + "\n");
                }
            }
            else
            {
                int appInit = content.IndexOf("const app = Fastify", StringComparison.Ordinal);
                int lineEnd = appInit >= 0 ? content.IndexOf('\n', appInit) : -1;
                if (lineEnd > 0)
                {
                    content = content.Insert(lineEnd + 1, callLine + "\n");
                }
            }
        }

        File.WriteAllText(fastifyPath, content);
    }

    private static string ToPascal(string value)
    {
        string[] parts = value.Split(new[] { '-', '_', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
