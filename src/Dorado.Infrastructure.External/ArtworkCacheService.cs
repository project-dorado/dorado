using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Content-addressed on-disk artwork cache. Downloads remote artwork once and serves
/// subsequent requests from local files so the UI only ever binds local paths.
/// </summary>
public sealed class ArtworkCacheService : IArtworkCacheService
{
    private const string UserAgent = "Dorado/1.0 (+https://github.com/project-dorado/dorado)";

    private readonly HttpClient _http;

    public string CacheDirectory { get; }

    public ArtworkCacheService(HttpMessageHandler? innerHandler = null, string? cacheDirectory = null, TimeSpan? rateLimitInterval = null)
    {
        CacheDirectory = cacheDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dorado", "cache", "artwork");
        Directory.CreateDirectory(CacheDirectory);

        var interval = rateLimitInterval ?? TimeSpan.FromSeconds(1.05);
        var handler = new RateLimitedHttpMessageHandler(innerHandler ?? new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        }, interval);
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<string?> GetOrDownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var cachePath = BuildCachePath(url, uri);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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
            if (bytes.Length == 0)
            {
                return null;
            }

            var tempPath = cachePath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
    }

    private string BuildCachePath(string url, Uri uri)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension) || extension.Length > 6 || !extension.StartsWith('.'))
        {
            extension = ".jpg";
        }

        return Path.Combine(CacheDirectory, key + extension);
    }
}
