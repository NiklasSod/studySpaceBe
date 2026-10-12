using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace lmsPortalBe.Services
{
  public interface ICloudinaryAudioService
  {
    /// <summary>
    /// Uploads an audio file to Cloudinary and returns its secure HTTPS URL.
    /// </summary>
    Task<string> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Cloudinary public ID behind an audio URL returned by
    /// <see cref="UploadAsync"/>, or <c>null</c> when the URL does not point at a
    /// raw upload in this Cloudinary account.
    /// </summary>
    string? GetPublicId(string url);

    /// <summary>
    /// Deletes a previously uploaded audio asset. Returns <c>false</c> when
    /// Cloudinary could not process the request; deleting an asset that is
    /// already gone counts as success.
    /// </summary>
    Task<bool> DeleteAsync(string publicId);
  }

  public class CloudinaryAudioService : ICloudinaryAudioService
  {
    private const string Folder = "voice-notes";
    private const string Host = "res.cloudinary.com";

    private readonly Lazy<string?> _cloudName;
    private readonly ILogger<CloudinaryAudioService> _logger;
    private readonly Lazy<Cloudinary> _cloudinary;

    public CloudinaryAudioService(IConfiguration configuration, ILogger<CloudinaryAudioService> logger)
    {
      _logger = logger;
      _cloudName = new Lazy<string?>(() => configuration["CLOUDINARY_CLOUD_NAME"]);
      // Build the Cloudinary client lazily so the application can still boot
      // (e.g. in tests) when the credentials are not configured. The upload
      // endpoint is the only place that ever touches it.
      _cloudinary = new Lazy<Cloudinary>(() => CreateCloudinary(configuration));
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
      var uploadParams = new RawUploadParams
      {
        File = new FileDescription(fileName, stream),
        Folder = Folder,
        UseFilename = true,
        UniqueFilename = true
      };

      var result = await _cloudinary.Value.UploadAsync(uploadParams, "raw", cancellationToken);

      if (result.Error is not null)
      {
        throw new InvalidOperationException(result.Error.Message);
      }

      return result.SecureUrl.AbsoluteUri;
    }

    public string? GetPublicId(string url)
    {
      if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
          || uri.Scheme != Uri.UriSchemeHttps
          || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      // Delivery URLs look like /{cloud}/raw/upload/[v{version}/]{publicId}.
      var segments = uri.AbsolutePath.Trim('/').Split('/');
      var cloudName = _cloudName.Value;
      if (string.IsNullOrWhiteSpace(cloudName)
          || segments.Length < 4
          || !string.Equals(Uri.UnescapeDataString(segments[0]), cloudName, StringComparison.OrdinalIgnoreCase)
          || !string.Equals(segments[1], "raw", StringComparison.OrdinalIgnoreCase)
          || !string.Equals(segments[2], "upload", StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      // The version segment is only present once an asset has been (re)uploaded.
      var firstPublicIdSegment = segments[3].Length > 1
          && segments[3][0] == 'v'
          && long.TryParse(segments[3].AsSpan(1), out _)
              ? 4
              : 3;

      var publicId = string.Join('/', segments.Skip(firstPublicIdSegment).Select(Uri.UnescapeDataString));
      return string.IsNullOrWhiteSpace(publicId) ? null : publicId;
    }

    public async Task<bool> DeleteAsync(string publicId)
    {
      try
      {
        var result = await _cloudinary.Value.DestroyAsync(new DeletionParams(publicId)
        {
          ResourceType = ResourceType.Raw,
          Invalidate = true
        });

        if (result.Error is not null)
        {
          _logger.LogWarning(
              "Cloudinary rejected deleting audio asset {PublicId}: {Message}",
              publicId,
              result.Error.Message);
          return false;
        }

        return true;
      }
      catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
      {
        // Removing a recording must never fail the surrounding request.
        _logger.LogWarning(ex, "Cloudinary could not delete audio asset {PublicId}.", publicId);
        return false;
      }
    }

    private static Cloudinary CreateCloudinary(IConfiguration configuration)
    {
      var cloudName = configuration["CLOUDINARY_CLOUD_NAME"];
      var apiKey = configuration["CLOUDINARY_API_KEY"];
      var apiSecret = configuration["CLOUDINARY_API_SECRET"];

      if (string.IsNullOrWhiteSpace(cloudName)
          || string.IsNullOrWhiteSpace(apiKey)
          || string.IsNullOrWhiteSpace(apiSecret))
      {
        throw new InvalidOperationException(
            "Cloudinary is not configured. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY and CLOUDINARY_API_SECRET.");
      }

      return new Cloudinary(new Account(cloudName, apiKey, apiSecret));
    }
  }
}
