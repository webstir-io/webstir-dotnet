using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Bridge.Frontend;

public static class SubresourceIntegrity
{
    public static string Compute(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] hash = SHA384.HashData(content);
        string base64 = Convert.ToBase64String(hash);
        return $"sha384-{base64}";
    }

    public static async Task<string?> ComputeForUrlAsync(HttpClient http, string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Non-success status codes are expected for external resources
                return null;
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return Compute(bytes);
        }
        catch
        {
            // SRI is optional - don't fail the build if we can't fetch the resource
            return null;
        }
    }
}
