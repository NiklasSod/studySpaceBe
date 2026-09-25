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
  }

  public class CloudinaryAudioService : ICloudinaryAudioService
  {
    private const string Folder = "voice-notes";

    private readonly Lazy<Cloudinary> _cloudinary;

    public CloudinaryAudioService(IConfiguration configuration)
    {
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
