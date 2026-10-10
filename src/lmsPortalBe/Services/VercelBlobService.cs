using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace lmsPortalBe.Services
{
  public interface IVercelBlobService
  {
    /// <summary>
    /// Uploads an image to Vercel Blob and returns its public HTTPS URL.
    /// </summary>
    Task<string> UploadAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default);
  }

  public class VercelBlobService : IVercelBlobService
  {
    private const string ApiBaseUrl = "https://vercel.com/api/blob";
    private const string ApiVersion = "12";
    private const string Folder = "interactive-images";

    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly Lazy<(string Token, string StoreId)> _credentials;

    public VercelBlobService(IConfiguration configuration)
    {
      _configuration = configuration;
      _httpClient = new HttpClient();
      _credentials = new Lazy<(string Token, string StoreId)>(ResolveCredentials);
    }

    public async Task<string> UploadAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
      var (token, storeId) = _credentials.Value;

      var pathname = $"{Folder}/{SanitizeFileName(fileName)}";
      var url = $"{ApiBaseUrl}/?pathname={Uri.EscapeDataString(pathname)}";

      using var request = new HttpRequestMessage(HttpMethod.Put, url);
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      request.Headers.TryAddWithoutValidation("x-api-version", ApiVersion);
      request.Headers.TryAddWithoutValidation("x-vercel-blob-store-id", storeId);
      request.Headers.TryAddWithoutValidation("x-vercel-blob-access", "public");
      request.Headers.TryAddWithoutValidation("x-content-type", contentType);

      request.Content = new ByteArrayContent(content);
      request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

      using var response = await _httpClient.SendAsync(request, cancellationToken);
      var body = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        throw new InvalidOperationException(
            $"Vercel Blob upload failed ({(int)response.StatusCode}): {body}");
      }

      var result = JsonSerializer.Deserialize<VercelBlobPutResponse>(body);
      if (string.IsNullOrWhiteSpace(result?.Url))
      {
        throw new InvalidOperationException(
            $"Vercel Blob upload returned no URL. Response: {body}");
      }

      return result.Url;
    }

    private (string Token, string StoreId) ResolveCredentials()
    {
      var token = _configuration["BLOB_READ_WRITE_TOKEN"];
      if (string.IsNullOrWhiteSpace(token))
      {
        throw new InvalidOperationException(
            "Vercel Blob is not configured. Set BLOB_READ_WRITE_TOKEN.");
      }

      // The store id is embedded in the read-write token (4th '_'-separated
      // segment), but an explicit BLOB_STORE_ID takes precedence.
      var storeId = _configuration["BLOB_STORE_ID"];
      if (string.IsNullOrWhiteSpace(storeId))
      {
        var parts = token.Split('_');
        storeId = parts.Length > 3 ? parts[3] : string.Empty;
      }

      if (storeId.StartsWith("store_", StringComparison.Ordinal))
      {
        storeId = storeId["store_".Length..];
      }

      if (string.IsNullOrWhiteSpace(storeId))
      {
        throw new InvalidOperationException(
            "Could not determine the Vercel Blob store id. Set BLOB_STORE_ID or use a valid BLOB_READ_WRITE_TOKEN.");
      }

      return (token, storeId);
    }

    private static string SanitizeFileName(string fileName)
    {
      var name = Path.GetFileName(fileName);
      foreach (var invalid in Path.GetInvalidFileNameChars())
      {
        name = name.Replace(invalid, '-');
      }

      return $"{Guid.NewGuid():N}-{name}";
    }

    private sealed class VercelBlobPutResponse
    {
      [JsonPropertyName("url")]
      public string Url { get; set; } = string.Empty;
    }
  }
}
