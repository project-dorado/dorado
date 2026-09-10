using System.Net.Http.Headers;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Cover Art Archive client: builds front-art URLs for release groups and downloads resolved artwork bytes.
/// </summary>
public sealed class CoverArtArchiveClient
{
    private readonly HttpClient _http;

    public CoverArtArchiveClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public string BuildFrontUrl(string releaseGroupMbid, string size = "front-250")
    {
        return $"https://coverartarchive.org/release-group/{releaseGroupMbid}/{size}";
    }

    public async Task<byte[]?> DownloadFrontAsync(string releaseGroupMbid, string size, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http
                .GetAsync(BuildFrontUrl(releaseGroupMbid, size), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return null;
        }
    }
}
