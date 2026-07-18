using System.Net.Http;
using System.Text.Json;

namespace AppiumSetupManager.Core.Services;

public interface IUpdateCheckService
{
    /// <summary>
    /// Looks up the latest published version of an npm package from the public npm registry.
    /// Returns null on ANY failure — non-success status, timeout, network error, or malformed/missing
    /// JSON — never throws. Best-effort only; the Updates screen falls back to its own baseline
    /// floor-based classification when this returns null.
    /// </summary>
    Task<string?> GetLatestNpmVersionAsync(string npmPackageName, CancellationToken ct = default);
}

public sealed class UpdateCheckService : IUpdateCheckService, IDisposable
{
    private readonly HttpClient _httpClient;

    public UpdateCheckService() : this(new HttpClientHandler())
    {
    }

    // Internal seam so tests can substitute a fake HttpMessageHandler without a mocking library —
    // production code always goes through the public parameterless constructor.
    internal UpdateCheckService(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    public async Task<string?> GetLatestNpmVersionAsync(string npmPackageName, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient
                .GetAsync($"https://registry.npmjs.org/{npmPackageName}/latest", ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("version", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.String)
                return null;

            var version = versionElement.GetString();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            // Covers both the 5-second HttpClient.Timeout expiring (surfaces as a
            // TaskCanceledException) and the caller's own token being cancelled — either way this is
            // a lookup failure the caller should treat as "unknown", not a propagated cancellation.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
