using System.Collections.Concurrent;
using lmsPortalBe.Services;

namespace lmsPortalBe.Tests;

/// <summary>
/// Test double for <see cref="ICloudinaryAudioService"/> that records the audio
/// assets the API deletes while reusing the real URL/public-ID parsing, so tests
/// can assert which recordings an update actually removed.
/// </summary>
public class RecordingCloudinaryAudioService(ICloudinaryAudioService inner) : ICloudinaryAudioService
{
  private readonly ConcurrentQueue<string> _deleted = new();

  /// <summary>Public IDs the API asked Cloudinary to delete.</summary>
  public IReadOnlyCollection<string> DeletedPublicIds => _deleted.ToArray();

  public Task<string> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default) =>
      inner.UploadAsync(stream, fileName, cancellationToken);

  public string? GetPublicId(string url) => inner.GetPublicId(url);

  public Task<bool> DeleteAsync(string publicId)
  {
    _deleted.Enqueue(publicId);
    return Task.FromResult(true);
  }
}
